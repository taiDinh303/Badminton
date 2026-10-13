using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Booking;
using Contract.Services.Group;
using Contract.Services.Points;
using Contract.Services.Pricing;
using Contract.Services.Voucher;
using M.Core.Base;
using M.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelViews.Booking;

namespace Services
{
    public sealed class BookingService : BaseService, IBookingService
    {
        private readonly IPricingService _pricing;
        private readonly IVoucherService _vouchers;
        private readonly IGroupService _groups;
        private readonly IAuditService _audit;
        private readonly IPointService _points;

        public BookingService(IUnitOfWork uow, IHttpContextAccessor http,
            IPricingService pricing, IVoucherService vouchers, IGroupService groups,
            IAuditService audit, IPointService points)
            : base(uow, http)
        {
            _pricing = pricing;
            _vouchers = vouchers;
            _groups = groups;
            _audit = audit;
            _points = points;
        }

        private IGenericRepository<Booking> Repo => Repository<Booking>();

        public async Task<BookingView> CreateAsync(CreateBookingRequest request, int userId)
        {
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existing = (await Repo.GetByConditionAsync(
                    b => b.IdempotencyKey == request.IdempotencyKey)).FirstOrDefault();
                if (existing is not null) return await ToView(existing);
            }

            var court = await Repository<Court>().GetByIdAsync(request.CourtId);
            if (court is null || !court.IsActive)
                throw new BadRequestException("San khong ton tai hoac khong hoat dong.");

            var slot = await Repository<TimeSlot>().GetByIdAsync(request.SlotId);
            if (slot is null || !slot.IsActive)
                throw new BadRequestException("Khung gio khong ton tai.");

            var date = ParseDate(request.Date);

            var dateStatus = await GetCourtDateStatus(court.Id, date);
            if (dateStatus != CourtDateStatus.Open)
                throw new ConflictException("San dang bao tri hoac dong trong ngay nay.");

            var now = DateTime.UtcNow;
            var slotStart = date.ToDateTime(slot.StartTime);
            if (slotStart < now.AddMinutes(30))
                throw new BadRequestException(
                    "Thoi gian nay khong the dat (slot da bat dau hoac trong 30 phut toi).");
            if (date > DateOnly.FromDateTime(now.Date.AddDays(30)))
                throw new BadRequestException("Vui long chon ngay trong 30 ngay toi.");

            if (request.GroupId is { } groupId)
            {
                if (!await _groups.CanBookForGroupAsync(groupId, userId))
                    throw new ConflictException("Chi nguoi tao nhom duoc dat cho nhom.");
                if (await CountActiveBookings(userId, groupId, date) >= 8)
                    throw new ConflictException("Nhom da dat giới han 8 slot/ngay.");
            }
            else
            {
                if (await CountActiveBookings(userId, null, date) >= 4)
                    throw new ConflictException("Ban da dat giới han 4 slot/ngay.");
            }

            int voucherDiscount = 0;
            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                var baseForVoucher = (int)_pricing.WithWeekend(
                    court.PriceLowPeak, court.PricePeak, slot.IsPeak,
                    court.WeekendSurcharge, IsWeekend(date));
                voucherDiscount = await _vouchers.PreviewDiscountAsync(
                    request.VoucherCode, userId, baseForVoucher);
                if (voucherDiscount == 0)
                    throw new BadRequestException("Voucher khong ap dung cho don nay.");
            }

            var total = (int)_pricing.WithWeekend(
                court.PriceLowPeak, court.PricePeak, slot.IsPeak,
                court.WeekendSurcharge, IsWeekend(date));
            var finalAmount = (int)_pricing.FinalPrice(
                court.PriceLowPeak, court.PricePeak, slot.IsPeak,
                court.WeekendSurcharge, IsWeekend(date), voucherDiscount);

            var booking = new Booking
            {
                BookingCode = GenerateCode(),
                IdempotencyKey = request.IdempotencyKey,
                UserId = userId,
                GroupId = request.GroupId,
                CourtId = court.Id,
                SlotId = slot.Id,
                BranchId = court.BranchId,
                Date = date,
                PlayerCount = Math.Max(1, request.PlayerCount),
                Status = BookingStatus.Pending,
                TotalAmount = total,
                DiscountAmount = voucherDiscount,
                PayableAmount = finalAmount,
                PendingExpiresAt = now.AddMinutes(10),
                Note = request.Note
            };

            await PersistAsync(booking, userId);
            return await ToView(booking);
        }

        private async Task PersistAsync(Booking booking, int creatorId)
        {
            _ = Repo.AddAsync(booking);
            var user = await Repository<User>().GetByIdAsync(creatorId);
            var bp = new BookingPlayer
            {
                UserId = creatorId,
                IsCreator = true,
                Booking = booking,
                User = user
            };
            _ = Repository<BookingPlayer>().AddAsync(bp);

            try { await SaveAsync(); }
            catch (DbUpdateException ex)
            {
                if (ex.Message.Contains("ux_court_slot_active") ||
                    ex.InnerException?.Message?.Contains("UNIQUE") is true ||
                    ex.InnerException?.Message?.Contains("unique") is true)
                    throw new ConflictException(
                        "SLOT_CONFLICT: san nay gio vay vua duoc dat, hay chon khung khac.");
                throw;
            }
        }

        // - read ------------------------------------------------------------

        public async Task<BookingView> GetByIdAsync(int id, int requestingUserId)
        {
            var b = await Repo.GetByIdAsync(id);
            if (b is null || b.DeletedAt.HasValue)
                throw new NotFoundException("Booking", id);
            await AuthorizeView(b, requestingUserId);
            return await ToView(b);
        }

        public async Task<IReadOnlyList<BookingView>> GetByUserAsync(int userId, string? status = null)
        {
            var bookings = await Repo.GetByConditionAsync(b => b.UserId == userId && b.DeletedAt == null);
            if (!string.IsNullOrWhiteSpace(status))
                bookings = bookings
                    .Where(b => b.Status.ToString().Equals(status, System.StringComparison.OrdinalIgnoreCase))
                    .ToList();

            // Bulk-fetch the reference data so we can map sequentially on the
            // scoped DbContext (no parallel ToView calls hitting the same context).
            var courtIds = bookings.Select(b => b.CourtId).Distinct().ToList();
            var slotIds = bookings.Select(b => b.SlotId).Distinct().ToList();
            var userIds = bookings.Select(b => b.UserId).Distinct().ToList();

            var courts = (await Repository<Court>().GetByConditionAsync(c => courtIds.Contains(c.Id)))
                .ToDictionary(c => c.Id);
            var slots = (await Repository<TimeSlot>().GetByConditionAsync(s => slotIds.Contains(s.Id)))
                .ToDictionary(s => s.Id);
            var users = (await Repository<User>().GetByConditionAsync(u => userIds.Contains(u.Id)))
                .ToDictionary(u => u.Id);

            var views = new List<BookingView>(bookings.Count);
            foreach (var b in bookings)
            {
                if (!courts.TryGetValue(b.CourtId, out var court)
                    || !slots.TryGetValue(b.SlotId, out var slot))
                    throw new NotFoundException("Booking data", b.Id);

                var isWeekend = IsWeekend(b.Date);
                var basePrice = (int)_pricing.WithWeekend(
                    court.PriceLowPeak, court.PricePeak, slot.IsPeak,
                    court.WeekendSurcharge, isWeekend);
                var weekendOnly = isWeekend ? basePrice : 0;
                var breakdown = new BookingPriceBreakdown(
                    basePrice, weekendOnly, b.DiscountAmount, b.TotalAmount, b.PayableAmount);
                users.TryGetValue(b.UserId, out var user);

                views.Add(new BookingView(
                    b.Id, b.BookingCode, b.CourtId, court.Name,
                    b.Date.ToString("dd/MM/yyyy"),
                    $"{slot.Label} ({slot.StartTime:hh\\:mm}-{slot.EndTime:hh\\:mm})",
                    b.PlayerCount, b.Status.ToString(), b.TotalAmount, b.DiscountAmount,
                    b.PayableAmount, b.PointsEarned, b.GroupId, b.VoucherRedemptionId,
                    b.CreatedAt, user?.FullName, breakdown, b.CancelReason));
            }
            return views;
        }

        // - cancel (slot release; tier applied by PaymentService) ----------

        public async Task<BookingView> CancelAsync(int bookingId, int actorId)
        {
            var b = await Repo.GetByIdAsync(bookingId);
            if (b is null || b.DeletedAt.HasValue)
                throw new NotFoundException("Booking", bookingId);
            await AuthorizeCancel(b, actorId);

            if (b.Status is BookingStatus.Cancelled or BookingStatus.NoShow or BookingStatus.Refunded)
                throw new ConflictException($"Dat cho da o trang thai {b.Status}.");

            var before = new { b.Status, b.CancelReason };
            b.Status = BookingStatus.Cancelled;
            b.CancelledAt = DateTime.UtcNow;
            b.CancelReason = "Khach/manager huy";
            b.UpdatedAt = DateTime.UtcNow;
            Repo.Update(b);
            await SaveAsync();
            await _audit.LogAsync(actorId, "booking.cancel", "Booking", b.Id.ToString(),
                before, new { b.Status, b.CancelledAt }, b.CancelReason);

            return await ToView(b);
        }

        // - availability grid ----------------------------------------------

        public async Task<IReadOnlyList<CourtAvailabilityView>> GetAvailabilityAsync(int branchId, DateOnly date)
        {
            var courts = await Repository<Court>().GetByConditionAsync(
                c => c.BranchId == branchId && c.IsActive && c.DeletedAt == null);
            var slots = await Repository<TimeSlot>().GetByConditionAsync(
                s => s.IsActive && s.DeletedAt == null);
            var now = DateTime.UtcNow;

            var views = new List<CourtAvailabilityView>();
            foreach (var c in courts)
            {
                var dayStatus = await GetCourtDateStatus(c.Id, date);
                var bookedSlots = (await Repo.GetByConditionAsync(b =>
                        b.CourtId == c.Id && b.Date == date &&
                        (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)))
                    .Select(b => b.SlotId).ToHashSet();

                var slotViews = slots.Select(s =>
                {
                    var start = date.ToDateTime(s.StartTime);
                    var isLocked = dayStatus != CourtDateStatus.Open
                        || bookedSlots.Contains(s.Id)
                        || start < now.AddMinutes(30);
                    var lockReason = isLocked
                        ? (dayStatus != CourtDateStatus.Open ? "San bao tri/dong"
                          : bookedSlots.Contains(s.Id) ? "Da duoc dat"
                          : "Trong qua khuch")
                        : "";
                    return new SlotView(s.Id, s.Label,
                        s.StartTime.ToString("hh\\:mm"), s.EndTime.ToString("hh\\:mm"),
                        s.IsPeak, isLocked, lockReason);
                }).ToList();

                views.Add(new CourtAvailabilityView(
                    c.Id, c.Code, c.Name, c.PriceLowPeak, c.PricePeak, slotViews));
            }
            return views;
        }

        // - helpers ---------------------------------------------------------

        private async Task<int> CountActiveBookings(int userId, int? groupId, DateOnly date)
        {
            var u = userId; var g = groupId;
            var active = await Repo.GetByConditionAsync(b =>
                b.Date == date && b.UserId == u &&
                ((b.GroupId == null && g == null) || b.GroupId == g) &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            return active.Count;
        }

        private async Task<CourtDateStatus> GetCourtDateStatus(int courtId, DateOnly date)
        {
            var rows = await Repository<CourtDateStatusEntry>().GetByConditionAsync(
                r => r.CourtId == courtId && r.Date == date);
            return rows.FirstOrDefault()?.Status ?? CourtDateStatus.Open;
        }

        private async Task AuthorizeView(Booking b, int requestingUserId)
        {
            if (b.UserId == requestingUserId) return;
            if (b.GroupId is { } gid && await _groups.CanBookForGroupAsync(gid, requestingUserId)) return;
            // manager/admin scope enforced at the controller via [Authorize(Roles)]
            throw new ConflictException("Khong co quyen xem dat cho nay.");
        }

        private async Task AuthorizeCancel(Booking b, int actorId)
        {
            if (b.GroupId is { } gid && await _groups.CanBookForGroupAsync(gid, actorId)) return;
            if (b.UserId == actorId) return;
            // manager/admin override handled by [Authorize(Roles)] at the controller.
        }

        private async Task<BookingView> ToView(Booking b)
        {
            var court = await Repository<Court>().GetByIdAsync(b.CourtId);
            var slot = await Repository<TimeSlot>().GetByIdAsync(b.SlotId);
            var user = await Repository<User>().GetByIdAsync(b.UserId);
            if (court is null || slot is null)
                throw new NotFoundException("Booking data", b.Id);

            var isWeekend = IsWeekend(b.Date);
            var basePrice = (int)_pricing.WithWeekend(
                court.PriceLowPeak, court.PricePeak, slot.IsPeak,
                court.WeekendSurcharge, isWeekend);
            var weekendOnly = isWeekend ? basePrice : 0;
            var breakdown = new BookingPriceBreakdown(
                basePrice, weekendOnly, b.DiscountAmount, b.TotalAmount, b.PayableAmount);

            return new BookingView(
                b.Id, b.BookingCode, b.CourtId, court.Name,
                b.Date.ToString("dd/MM/yyyy"),
                $"{slot.Label} ({slot.StartTime:hh\\:mm}-{slot.EndTime:hh\\:mm})",
                b.PlayerCount, b.Status.ToString(), b.TotalAmount, b.DiscountAmount,
                b.PayableAmount, b.PointsEarned, b.GroupId, b.VoucherRedemptionId,
                b.CreatedAt, user?.FullName, breakdown, b.CancelReason);
        }

        private static bool IsWeekend(DateOnly d) =>
            d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        private static DateOnly ParseDate(string s) =>
            DateOnly.TryParse(s, out var d) ? d : throw new BadRequestException("Ngay khong hop le.");

        private static string GenerateCode() =>
            "BD-" + DateTime.UtcNow.ToString("yyMMdd") + "-" +
            Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }
}
