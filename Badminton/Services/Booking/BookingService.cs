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

            var total = (int)_pricing.WithWeekend(
                court.PriceLowPeak, court.PricePeak, slot.IsPeak,
                court.WeekendSurcharge, IsWeekend(date));

            // The whole booking (order + player + voucher reservation) is
            // all-or-nothing: a voucher that fails its total-qty guard rolls the
            // order back with it, and vice-versa.
            Booking? booking = null;
            await Uow.RunInTransactionAsync(async () =>
            {
                int voucherDiscount = 0;
                VoucherRedemption? voucherRedemption = null;

                if (!string.IsNullOrWhiteSpace(request.VoucherCode))
                {
                    (voucherDiscount, voucherRedemption) =
                        await _vouchers.ReserveVoucherAsync(
                            request.VoucherCode, userId, total);
                }

                var finalAmount = (int)_pricing.FinalPrice(
                    court.PriceLowPeak, court.PricePeak, slot.IsPeak,
                    court.WeekendSurcharge, IsWeekend(date), voucherDiscount);

                var b = new Booking
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

                // Cross-links (booking.VoucherRedemptionId and
                // redemption.BookingId) stay null until both rows have
                // primary keys; they are patched after the first save below.

                // In-transaction slot pre-check: a second concurrent order for
                // the same court+date+slot in an active state means the slot
                // just got taken. The filtered unique index is the hard
                // backstop for any race that slips past this read.
                var slotTaken = await Repo.GetByConditionAsync(x =>
                    x.CourtId == court.Id && x.Date == date && x.SlotId == slot.Id
                    && (x.Status == BookingStatus.Pending || x.Status == BookingStatus.Confirmed));
                if (slotTaken.Count > 0)
                    throw new ConflictException(
                        "SLOT_CONFLICT: san nay gio vay vua duoc dat, hay chon khung khac.");

                _ = Repo.AddAsync(b);
                var user = await Repository<User>().GetByIdAsync(userId);
                _ = Repository<BookingPlayer>().AddAsync(new BookingPlayer
                {
                    UserId = userId,
                    IsCreator = true,
                    Booking = b,
                    User = user
                });

                try { await SaveAsync(); }
                catch (DbUpdateException ex)
                {
                    if (IsSlotConflict(ex))
                        throw new ConflictException(
                            "SLOT_CONFLICT: san nay gio vay vua duoc dat, hay chon khung khac.");
                    throw;
                }

                // Patch the cross-links now that both rows have primary keys.
                if (voucherRedemption is not null)
                {
                    voucherRedemption.BookingId = b.Id;
                    b.VoucherRedemptionId = voucherRedemption.Id;
                    await SaveAsync();
                }

                booking = b;
            });

            // The transaction only completes without throwing when the order
            // was persisted, so a null here would mean a silent failure.
            return await ToView(booking!);
        }

        private static bool IsSlotConflict(DbUpdateException ex) =>
            ex.Message.Contains("ux_court_slot_active")
            || ex.InnerException?.Message?.Contains("UNIQUE") is true
            || ex.InnerException?.Message?.Contains("unique") is true
            || ex.InnerException?.Message?.Contains("IX_Bookings_CourtId_Date_SlotId") is true;

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
            // Backward-compatible: load the (possibly status-filtered) list for
            // this user, newest first. Paged callers use GetByUserPagedAsync.
            var bookings = await Repo.GetByConditionAsync(b => b.UserId == userId && b.DeletedAt == null);
            if (!string.IsNullOrWhiteSpace(status))
            {
                var st = status;
                bookings = bookings
                    .Where(b => b.Status.ToString().Equals(st, System.StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(b => b.CreatedAt)
                    .ToList();
            }
            else
            {
                bookings = bookings.OrderByDescending(b => b.CreatedAt).ToList();
            }
            return await MapToViewsAsync(bookings);
        }

        /// <summary>
        /// Paged "my bookings" — server-side <c>Skip/Take</c> + total count in
        /// one round trip (replaces the unbounded full-list fetch for the
        /// customer dashboard). Status filter is pushed into the query.
        /// </summary>
        public async Task<(IReadOnlyList<BookingView> Items, int TotalCount)>
            GetByUserPagedAsync(int userId, string? status, Page page)
        {
            var u = userId;
            string? st = status;
            System.Linq.Expressions.Expression<Func<Booking, bool>> predicate =
                b => b.UserId == u && b.DeletedAt == null
                     && (st == null || b.Status.ToString().Equals(st));

            var (bookings, total) = await Repo.GetPagedAsync(
                predicate, b => b.CreatedAt, OrderDirection.Desc, page);
            var views = await MapToViewsAsync(bookings.ToList());
            return (views, total);
        }

        /// <summary>
        /// Shared mapper: bulk-fetches reference data for a set of bookings so
        /// each row maps without issuing its own queries (anti-N+1).
        /// </summary>
        private async Task<IReadOnlyList<BookingView>> MapToViewsAsync(IReadOnlyList<Booking> bookings)
        {
            if (bookings.Count == 0) return Array.Empty<BookingView>();

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

            // Release a reserved voucher (pre-payment cancel) so its "luot"
            // goes back to the pool. Runs inside the same transaction as the
            // slot release so the two are all-or-nothing.
            var redemption = b.VoucherRedemptionId is { } rid
                ? await Repository<VoucherRedemption>().GetByIdAsync(rid)
                : null;

            var before = new { b.Status, b.CancelReason };
            b.Status = BookingStatus.Cancelled;
            b.CancelledAt = DateTime.UtcNow;
            b.CancelReason = "Khach/manager huy";
            b.UpdatedAt = DateTime.UtcNow;
            Repo.Update(b);

            if (redemption is not null)
                await _vouchers.ReleaseVoucherAsync(b.VoucherRedemptionId!.Value);

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

            // ANTI-N+1: fetch every court-date-status entry and every active
            // booking for this date in two batched queries, then map in-memory
            // (the old code issued 1 query per court for each of the two).
            var courtIds = courts.Select(c => c.Id).ToList();
            var dateStatusRows = await Repository<CourtDateStatusEntry>().GetByConditionAsync(
                r => r.Date == date && courtIds.Contains(r.CourtId));
            var statusByCourt = dateStatusRows
                .ToDictionary(r => r.CourtId, r => r.Status);

            var bookedRows = await Repo.GetByConditionAsync(b =>
                b.Date == date && courtIds.Contains(b.CourtId)
                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            var bookedByCourt = bookedRows
                .GroupBy(b => b.CourtId)
                .ToDictionary(g => g.Key, g => g.Select(b => b.SlotId).ToHashSet());

            var views = new List<CourtAvailabilityView>(courts.Count);
            foreach (var c in courts)
            {
                var dayStatus = statusByCourt.TryGetValue(c.Id, out var s) ? s : CourtDateStatus.Open;
                var bookedSlots = bookedByCourt.TryGetValue(c.Id, out var set) ? set : default;
                bookedSlots ??= new HashSet<int>();

                var slotViews = slots.Select(sl =>
                {
                    var start = date.ToDateTime(sl.StartTime);
                    var isLocked = dayStatus != CourtDateStatus.Open
                        || bookedSlots.Contains(sl.Id)
                        || start < now.AddMinutes(30);
                    var lockReason = isLocked
                        ? (dayStatus != CourtDateStatus.Open ? "San bao tri/dong"
                          : bookedSlots.Contains(sl.Id) ? "Da duoc dat"
                          : "Trong qua khuch")
                        : "";
                    return new SlotView(sl.Id, sl.Label,
                        sl.StartTime.ToString("hh\\:mm"), sl.EndTime.ToString("hh\\:mm"),
                        sl.IsPeak, isLocked, lockReason);
                }).ToList();

                views.Add(new CourtAvailabilityView(
                    c.Id, c.Code, c.Name, c.PriceLowPeak, c.PricePeak, slotViews));
            }
            return views;
        }

        // - helpers ---------------------------------------------------------

        private async Task<int> CountActiveBookings(int userId, int? groupId, DateOnly date)
        {
            // Server-side COUNT — no materialising the bookings list.
            var u = userId; var g = groupId;
            return await Repo.CountByConditionAsync(b =>
                b.Date == date && b.UserId == u &&
                ((b.GroupId == null && g == null) || b.GroupId == g) &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
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
