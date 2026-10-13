using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Booking;
using M.Core.Base;
using M.Core.Domain;
using M.Core.Enums;
using Microsoft.AspNetCore.Http;
using ModelViews.Booking;
using ModelViews.TimeSlot;

namespace Services
{
    /// <summary>
    /// BOOKING service — the core domain flow. It composes the POLYMORPHISM
    /// fee-policy factory (pricing) and the slot-conflict rule (a court+date+slot
    /// can only be Reserved once). Everything writes through ONE
    /// <see cref="IUnitOfWork"/> so booking + details + payment commit atomically.
    /// </summary>
    public sealed class BookingService : BaseService, IBookingService
    {
        private readonly IFeePolicyFactory _feeFactory;

        public BookingService(
            IUnitOfWork uow,
            IHttpContextAccessor http,
            IFeePolicyFactory feeFactory) : base(uow, http)
        {
            _feeFactory = feeFactory;
        }

        private IGenericRepository<Booking> Repo => Repository<Booking>();

        public async Task<BookingView> CreateAsync(
            CreateBookingRequest request, int userId, string userName)
        {
            if (request is null || request.Slots is null || request.Slots.Count == 0)
                throw new BadRequestException("At least one slot is required.");

            var date = request.BookingDate;
            var slots = request.Slots.Distinct().ToList();

            // --- Load selected time slots ---
            var slotIds = slots.Select(s => s.TimeSlotId).Distinct().ToList();
            var timeSlots = await Repository<TimeSlot>().GetByConditionAsync(
                x => slotIds.Contains(x.Id) && x.IsActive && x.DeletedAt == null);
            if (timeSlots.Count != slotIds.Count)
                throw new BadRequestException("One or more time slots do not exist.");

            // --- Load selected courts ---
            var courtIds = slots.Select(s => s.CourtId).Distinct().ToList();
            var courts = await Repository<Court>().GetByConditionAsync(
                x => courtIds.Contains(x.Id) && x.IsActive
                     && x.Status == CourtStatus.Available && x.DeletedAt == null);
            if (courts.Count != courtIds.Count)
                throw new BadRequestException("One or more courts are not available.");

            // --- Conflict check: already-Reserved details for court+date+slot ---
            var conflicts = await Repository<BookingDetail>().GetByConditionAsync(
                d => d.BookingDate == date
                     && d.Status == BookingDetailStatus.Reserved
                     && courtIds.Contains(d.CourtId)
                     && slotIds.Contains(d.TimeSlotId));
            if (conflicts.Count > 0)
            {
                var busy = conflicts
                    .Select(c => $"court {c.CourtId} / slot {c.TimeSlotId}")
                    .Distinct().ToList();
                throw new ConflictException(
                    "Slots already reserved: " + string.Join(", ", busy));
            }

            // --- Pricing: POLYMORPHISM via the fee-policy factory ---
            var courtTypeIds = courts.Select(c => c.CourtTypeId).Distinct().ToList();
            var courtTypes = await Repository<CourtType>().GetByConditionAsync(
                x => courtTypeIds.Contains(x.Id));
            var typeNameById = courtTypes.ToDictionary(t => t.Id, t => t.Name);

            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var isPrimeTime = timeSlots.Any(t =>
                t.StartTime >= new TimeOnly(18, 0) && t.StartTime < new TimeOnly(21, 0));

            decimal total = 0m;
            var appliedPolicies = new List<string>();
            foreach (var c in courts)
            {
                var typeName = typeNameById.GetValueOrDefault(c.CourtTypeId, "Standard");
                var policy = _feeFactory.GetPolicy(typeName);
                var hourCount = slots
                    .Where(s => s.CourtId == c.Id)
                    .Sum(s => HoursForSlot(s.TimeSlotId, timeSlots));
                var feeCtx = new FeeContext
                {
                    PricePerHour = c.PricePerHour,
                    Hours = hourCount,
                    IsWeekend = isWeekend,
                    IsPrimeTime = isPrimeTime
                };
                total += policy.CalculateTotal(feeCtx);
                appliedPolicies.Add(policy.PolicyName);
            }
            var appliedPolicy = string.Join(" + ",
                appliedPolicies.Distinct().OrderBy(x => x));

            // --- Build the booking entity and its detail rows (all in one UoW) ---
            var booking = new Booking
            {
                BookingCode = Booking.GenerateCode(DateTime.UtcNow),
                UserId = userId,
                BookingDate = date,
                TotalAmount = total,
                Status = BookingStatus.Pending,
                Note = request.Note,
                AppliedFeePolicy = appliedPolicy
            };
            ApplyCreateAudit(booking);

            foreach (var s in slots)
            {
                var court = courts.First(c => c.Id == s.CourtId);
                var ts = timeSlots.First(t => t.Id == s.TimeSlotId);
                var hours = HoursForSlot(s.TimeSlotId, timeSlots);
                var detail = new BookingDetail
                {
                    BookingId = 0, // EF will set from navigation when the booking is added
                    CourtId = court.Id,
                    TimeSlotId = ts.Id,
                    BookingDate = date,
                    Price = court.PricePerHour * hours,
                    Status = BookingDetailStatus.Reserved,
                    Booking = booking     // navigation link; EF sets the FK on save
                };
                ApplyCreateAudit(detail);
                booking.Details.Add(detail);
            }

            // Pending payment (strategy fills it on confirmation).
            var payment = new Payment
            {
                BookingId = 0, // EF will set from navigation
                Method = PaymentMethod.Cash,
                Amount = total,
                Status = PaymentStatus.Pending,
                Booking = booking
            };
            ApplyCreateAudit(payment);
            booking.Payment = payment;

            _ = Repo.AddAsync(booking);
            await SaveAsync(); // EF assigns the generated booking id + fixes child FKs

            return await ToViewAsync(booking.Id);
        }

        public async Task<IReadOnlyList<BookingView>> GetByUserAsync(int userId)
        {
            var bookings = await Repo.GetByConditionAsync(
                b => b.UserId == userId && b.DeletedAt == null);
            var views = new List<BookingView>();
            foreach (var b in bookings)
                views.Add(await ToViewAsync(b.Id));
            return views;
        }

        public Task<BookingView> GetByIdAsync(int id, int? userId) =>
            ToViewAsync(id);

        public async Task CancelAsync(int id, int userId, string userName)
        {
            var b = await Repo.GetByIdAsync(id);
            if (b is null || b.DeletedAt.HasValue)
                throw new NotFoundException(nameof(Booking), id);
            if (b.UserId != userId)
                throw new ConflictException("Not your booking.");
            if (b.Status is BookingStatus.Completed or BookingStatus.Cancelled)
                throw new BadRequestException(
                    $"Cannot cancel a {b.Status.ToString().ToLowerInvariant()} booking.");

            b.Status = BookingStatus.Cancelled;
            ApplyUpdateAudit(b);
            Repo.Update(b);

            var detailRepo = Repository<BookingDetail>();
            var details = await detailRepo.GetByConditionAsync(
                d => d.BookingId == id && d.Status == BookingDetailStatus.Reserved);
            foreach (var d in details)
            {
                d.Status = BookingDetailStatus.Cancelled;
                ApplyUpdateAudit(d);
                detailRepo.Update(d);
            }
            await SaveAsync();
        }

        public async Task<IReadOnlyList<CourtSlotAvailabilityView>> GetFreeCourtsAsync(DateOnly date)
        {
            var courts = await Repository<Court>().GetByConditionAsync(
                c => c.IsActive && c.Status == CourtStatus.Available && c.DeletedAt == null);
            var allSlots = await Repository<TimeSlot>().GetByConditionAsync(
                s => s.IsActive && s.DeletedAt == null);
            var taken = await Repository<BookingDetail>().GetByConditionAsync(
                d => d.BookingDate == date && d.Status == BookingDetailStatus.Reserved);
            var takenKeys = taken.Select(t => (t.CourtId, t.TimeSlotId)).ToHashSet();

            var courtTypes = await Repository<CourtType>().GetAllAsync();
            var typeNameById = courtTypes.Where(t => t.DeletedAt == null)
                .ToDictionary(t => t.Id, t => t.Name);

            var result = courts
                .Select(c =>
                {
                    var free = allSlots
                        .Where(s => !takenKeys.Contains((c.Id, s.Id)))
                        .Select(s => new TimeSlotView(
                            s.Id, s.StartTime, s.EndTime,
                            $"{s.StartTime:hh\\:mm}-{s.EndTime:hh\\:mm}"))
                        .ToList();
                    return new CourtSlotAvailabilityView(
                        c.Id, c.CourtCode, c.CourtName, c.PricePerHour,
                        typeNameById.GetValueOrDefault(c.CourtTypeId, "Unknown"),
                        free);
                })
                .Where(r => r.FreeSlots.Count > 0)
                .ToList();
            return result;
        }

        // ---------- helpers ----------
        private static int HoursForSlot(int timeSlotId, IReadOnlyCollection<TimeSlot> allSlots)
        {
            var s = allSlots.FirstOrDefault(x => x.Id == timeSlotId);
            return s is null ? 1 : (int)Math.Max(1, (s.EndTime - s.StartTime).TotalHours);
        }

        private async Task<BookingView> ToViewAsync(int bookingId)
        {
            var b = await Repo.GetByIdAsync(bookingId);
            if (b is null) throw new NotFoundException(nameof(Booking), bookingId);

            var details = await Repository<BookingDetail>()
                .GetByConditionAsync(d => d.BookingId == bookingId);

            var courtIds = details.Select(d => d.CourtId).Distinct().ToList();
            var courtMap = (await Repository<Court>()
                .GetByConditionAsync(c => courtIds.Contains(c.Id)))
                .ToDictionary(c => c.Id, c => c.CourtName);

            var slotIds = details.Select(d => d.TimeSlotId).Distinct().ToList();
            var slotMap = (await Repository<TimeSlot>()
                .GetByConditionAsync(s => slotIds.Contains(s.Id)))
                .ToDictionary(s => s.Id, s => s);

            var user = await Repository<User>().GetByIdAsync(b.UserId);

            return new BookingView(
                b.Id, b.BookingCode, b.BookingDate, b.TotalAmount,
                b.Status, b.Note, b.AppliedFeePolicy, b.CreatedAt,
                user?.FullName,
                details.Select(d => new BookingDetailView(
                    d.CourtId,
                    courtMap.TryGetValue(d.CourtId, out var cn) ? cn : "?",
                    d.TimeSlotId,
                    slotMap.TryGetValue(d.TimeSlotId, out var ts) ? ts.StartTime : TimeOnly.MinValue,
                    slotMap.TryGetValue(d.TimeSlotId, out var ts2) ? ts2.EndTime : TimeOnly.MinValue,
                    d.Price)).ToList());
        }
    }
}
