using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Booking;
using Contract.Services.Group;
using Contract.Services.Notify;
using Contract.Services.Payment;
using Contract.Services.Points;
using Contract.Services.Pricing;
using Contract.Services.Voucher;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Badminton.Core.Security;
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
        private readonly IPaymentService _payments;
        private readonly INotifyService _notify;

        public BookingService(IUnitOfWork uow, ICurrentUser currentUser,
            IPricingService pricing, IVoucherService vouchers, IGroupService groups,
            IAuditService audit, IPointService points, IPaymentService payments,
            INotifyService notify)
            : base(uow, currentUser)
        {
            _pricing = pricing;
            _vouchers = vouchers;
            _groups = groups;
            _audit = audit;
            _points = points;
            _payments = payments;
            _notify = notify;
        }

        private IGenericRepository<Booking> Repo => Repository<Booking>();

        public async Task<BookingView> CreateAsync(CreateBookingRequest request, int userId)
        {
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existing = (await Repo.GetByConditionAsync(
                    b => b.IdempotencyKey == request.IdempotencyKey)).FirstOrDefault();
                if (existing is not null)
                {
                    if (existing.UserId != userId || existing.CourtId != request.CourtId || existing.SlotId != request.SlotId
                        || existing.Date != ParseDate(request.Date) || existing.GroupId != request.GroupId
                        || existing.PlayerCount != request.PlayerCount)
                        throw new ConflictException("IdempotencyKey đã được dùng cho một yêu cầu khác.");
                    return await ToView(existing);
                }
            }

            var court = await Repository<Court>().GetByIdAsync(request.CourtId);
            if (court is null || !court.IsActive)
                throw new BadRequestException("San khong ton tai hoac khong hoat dong.");

            var slot = await Repository<TimeSlot>().GetByIdAsync(request.SlotId);
            if (slot is null || !slot.IsActive)
                throw new BadRequestException("Khung gio khong ton tai.");

            var date = ParseDate(request.Date);

            var dateStatus = await GetCourtDateStatus(court.Id, date, slot);
            if (dateStatus != CourtDateStatus.Open)
                throw new ConflictException("San dang bao tri hoac dong trong ngay nay.");

            var hanoiNow = HanoiTime.HanoiNow();
            var slotStart = HanoiTime.SlotStart(date, slot.StartTime);
            if (slotStart < hanoiNow.AddMinutes(30))
                throw new BadRequestException(
                    "Thoi gian nay khong the dat (slot da bat dau hoac trong 30 phut toi).");
            var hanoiToday = HanoiTime.HanoiToday();
            if (date > hanoiToday.AddDays(30))
                throw new BadRequestException("Vui long chon ngay trong 30 ngay toi.");

            var requester = await Repository<User>().GetByIdAsync(userId);
            if (requester is null || requester.DeletedAt.HasValue || requester.Status != UserStatus.Active || !requester.IsVerified)
                throw new ConflictException("Tài khoản chưa được xác minh hoặc đã bị khóa.");
            if (requester is not null
                && requester.NoShowLimited
                && requester.NoShowLimitedPeriod is { } lp
                && lp == HanoiTime.HanoiToday().MonthStart())
                throw new ConflictException(
                    "Tai khoan dang bi gioi han dat sau 3 lan ko di (thang nay). Thang tiep se mo lai.");

            if (request.GroupId is { } groupId)
            {
                if (!await _groups.CanBookForGroupAsync(groupId, userId))
                    throw new ConflictException("Chi nguoi tao nhom duoc dat cho nhom.");
                if (await CountActiveBookings(userId, groupId, date) >= 8)
                    throw new ConflictException("Nhom da dat gi?i han 8 slot/ngay.");
            }
            else
            {
                if (await CountActiveBookings(userId, null, date) >= 4)
                    throw new ConflictException("Ban da dat gi?i han 4 slot/ngay.");
            }

            var perPerson = (int)_pricing.WithWeekend(
                court.PriceLowPeak, court.PricePeak, slot.IsPeak,
                court.WeekendSurcharge, IsWeekend(date));

            var playerCount = Math.Max(1, request.PlayerCount);
            // Order total = court-hour rate × number of players. The frontend
            // previews the voucher against this same base (per-person, group
            // booking), so preview and submit stay consistent. NOTE: docs 01 §3
            // describes the rate as VND/giờ (per court-hour); billing per
            // person is the policy applied here — confirm in 08-plan.
            var total = perPerson * playerCount;

            Booking? booking = null;
            await Uow.RunInTransactionAsync(async () =>
            {
                await LockCourtDateAsync(court.Id, date);
                if (await GetCourtDateStatus(court.Id, date, slot) != CourtDateStatus.Open)
                    throw new ConflictException("Sân bảo trì/đóng trong khung giờ này.");
                int voucherDiscount = 0;
                int pointsDiscount = 0;
                VoucherRedemption? voucherRedemption = null;

                if (!string.IsNullOrWhiteSpace(request.VoucherCode))
                {
                    (voucherDiscount, voucherRedemption) =
                        await _vouchers.ReserveVoucherAsync(
                            request.VoucherCode, userId, total);
                }

                int pointsRedeemedCount = 0;
                if (request.PointsToRedeem is { } ptsReq && ptsReq > 0)
                {
                    var afterVoucher = total - voucherDiscount;
                    // maxPointsThatFit = points whose 20k-per-1000 discount â‰¤ afterVoucher.
                    var maxPointsThatFit = (long)afterVoucher * 1_000 / 20_000;
                    pointsRedeemedCount = Math.Min(ptsReq, (int)maxPointsThatFit);
                    if (pointsRedeemedCount < 1_000)
                        pointsRedeemedCount = 0; // can't redeem less than one 1000-pt unit
                    pointsDiscount = pointsRedeemedCount * 20_000 / 1_000;
                }

                var finalAmount = Math.Max(0, total - voucherDiscount - pointsDiscount);

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
                    DiscountAmount = voucherDiscount + pointsDiscount,
                    PayableAmount = finalAmount,
                    PointsRedeemed = pointsRedeemedCount,
                    PendingExpiresAt = DateTime.UtcNow.AddMinutes(10),
                    Note = request.Note
                };

                // In-transaction slot pre-check
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
                catch (UniqueConstraintException ex)
                {
                    if (ex.ConstraintName == "ux_court_slot_active")
                        throw new ConflictException(
                            "SLOT_CONFLICT: san nay gio vay vua duoc dat, hay chon khung khac.");
                    throw;
                }

                // Patch cross-links now that both rows have primary keys.
                if (voucherRedemption is not null)
                {
                    voucherRedemption.BookingId = b.Id;
                    b.VoucherRedemptionId = voucherRedemption.Id;
                }

                if (b.PointsRedeemed > 0)
                    await _points.RedeemAsync(userId, b.Id, b.PointsRedeemed, b.BookingCode);

                if (voucherRedemption is not null || b.PointsRedeemed > 0)
                    await SaveAsync();

                booking = b;
            });

            return await ToView(booking!);
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

        public async Task<(IReadOnlyList<BookingView> Items, int TotalCount)>
            GetByUserPagedAsync(int userId, string? status, Page page)
        {
            var u = userId;

            bool hasStatus = !string.IsNullOrWhiteSpace(status);
            BookingStatus? stEnum = hasStatus
                && Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var p)
                ? p
                : null;
            if (hasStatus && stEnum is null)
                throw new BadRequestException($"Trang thai '{status}' khong hop le.");

            System.Linq.Expressions.Expression<Func<Booking, bool>> predicate =
                b => b.UserId == u && b.DeletedAt == null
                     && (stEnum == null || b.Status == stEnum!.Value);

            var (bookings, total) = await Repo.GetPagedAsync(
                predicate, b => b.CreatedAt, OrderDirection.Desc, page);
            var views = await MapToViewsAsync(bookings.ToList());
            return (views, total);
        }

        public async Task<(IReadOnlyList<BookingView> Items, int TotalCount)>
            GetAllPagedAsync(string? status, int? branchId, Page page)
        {
            var bi = await ResolveStaffBranchAsync(branchId);
            bool hasStatus = !string.IsNullOrWhiteSpace(status);
            BookingStatus? stEnum = hasStatus
                && Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var p)
                ? p
                : null;
            if (hasStatus && stEnum is null)
                throw new BadRequestException($"Trang thai '{status}' khong hop le.");

            System.Linq.Expressions.Expression<Func<Booking, bool>> predicate = b =>
                b.DeletedAt == null
                && (stEnum == null || b.Status == stEnum!.Value)
                && (bi == null || b.BranchId == bi);
            var (bookings, total) = await Repo.GetPagedAsync(
                predicate, b => b.CreatedAt, OrderDirection.Desc, page);
            var views = await MapToViewsAsync(bookings.ToList());
            return (views, total);
        }

        private async Task<IReadOnlyList<BookingView>> MapToViewsAsync(IReadOnlyList<Booking> bookings)
        {
            if (bookings.Count == 0) return Array.Empty<BookingView>();

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

                var basePrice = b.TotalAmount;
                const int weekendOnly = 0;
                var pointsVnd = b.PointsRedeemed * 20_000 / 1_000; // 1000 pts â†’ 20k
                var voucherVnd = b.DiscountAmount - pointsVnd;
                var breakdown = new BookingPriceBreakdown(
                    basePrice, weekendOnly, voucherVnd, pointsVnd,
                    b.TotalAmount, b.PayableAmount);
                users.TryGetValue(b.UserId, out var user);

                views.Add(new BookingView(
                    b.Id, b.BookingCode, b.CourtId, court.Name,
                    b.Date.ToString("dd/MM/yyyy"),
                    $"{slot.Label} ({slot.StartTime:hh\\:mm}-{slot.EndTime:hh\\:mm})",
                    b.PlayerCount, b.Status.ToString(), b.TotalAmount, b.DiscountAmount,
                    b.PayableAmount, b.PointsEarned, b.PointsRedeemed,
                    b.GroupId, b.VoucherRedemptionId,
                    b.CreatedAt, user?.FullName, breakdown, b.CancelReason, b.QrToken, b.CheckedInAt));
            }
            return views;
        }

        // - cancel (P0-1: paid orders go through a tiered refund) ----------

        public async Task CancelForMaintenanceAsync(int bookingId, int actorId)
        {
            var b = await Repo.GetByIdAsync(bookingId) ?? throw new NotFoundException("Booking", bookingId);
            await AuthorizeStaffBranchAsync(b.BranchId, actorId);
            if (b.Status == BookingStatus.Confirmed)
            {
                await _payments.RefundForMaintenanceAsync(bookingId, actorId, "Hoàn toàn phần do sân bảo trì/đóng");
                if (b.VoucherRedemptionId is { } rid) await _vouchers.ReleaseVoucherAsync(rid);
                return;
            }
            if (b.Status != BookingStatus.Pending) return;
            var now = DateTime.UtcNow;
            var claimed = await Uow.ExecuteSqlRawAsync(
                "UPDATE \"Bookings\" SET \"Status\"='Cancelled', \"CancelReason\"=@p0, \"CancelledAt\"=@p1, \"UpdatedAt\"=@p1 WHERE \"Id\"=@p2 AND \"Status\"='Pending'",
                "Sân bảo trì/đóng", now, bookingId);
            if (claimed == 0) throw new ConflictException("Đơn vừa thay đổi trạng thái. Lưu lại lịch để xử lý hoàn tiền.");
            b.Status = BookingStatus.Cancelled; b.CancelReason = "Sân bảo trì/đóng"; b.CancelledAt = now; b.UpdatedAt = now;
            Repo.Update(b);
            if (b.VoucherRedemptionId is { } pendingRid) await _vouchers.ReleaseVoucherAsync(pendingRid);
            if (b.PointsRedeemed > 0) await _points.RestoreAsync(b.UserId, b.Id, b.PointsRedeemed, b.BookingCode);
            await SaveAsync();
            await _audit.LogAsync(actorId, "booking.maintenance-cancel", "Booking", b.Id.ToString(), null, new { b.Status }, b.CancelReason);
        }

        public async Task<BookingView> CancelAsync(int bookingId, int actorId)
        {
            var b = await Repo.GetByIdAsync(bookingId);
            if (b is null || b.DeletedAt.HasValue)
                throw new NotFoundException("Booking", bookingId);
            await AuthorizeCancel(b, actorId);

            if (b.Status is BookingStatus.Cancelled or BookingStatus.NoShow or BookingStatus.Refunded)
                throw new ConflictException($"Dat cho da o trang thai {b.Status}.");

            if (b.Status is BookingStatus.Confirmed or BookingStatus.Completed)
            {
                if (b.VoucherRedemptionId is { } paidRid)
                    await _vouchers.ReleaseVoucherAsync(paidRid);

                var refund = await _payments.CancelRefundAsync(bookingId, actorId,
                    "Khach/manager huy don da thanh toan.");
                b.Status = BookingStatus.Cancelled;
                b.CancelledAt = DateTime.UtcNow;
                b.CancelReason = "Huy don da thanh toan (hoan tien theo quy dinh)";
                b.UpdatedAt = DateTime.UtcNow;
                Repo.Update(b);
                await SaveAsync();
                await _audit.LogAsync(actorId, "booking.cancel-paid", "Booking", b.Id.ToString(),
                    new { b.Status },
                    new { Status = b.Status, RefundGatewayRef = refund.GatewayRef, RefundAmount = refund.RefundAmount },
                    b.CancelReason);
                return await ToView(b);
            }

            // Unpaid (Pending) path: just release the slot + voucher + points.
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

            if (b.PointsRedeemed > 0)
                await _points.RestoreAsync(b.UserId, b.Id, b.PointsRedeemed, b.BookingCode);

            await SaveAsync();
            await _audit.LogAsync(actorId, "booking.cancel", "Booking", b.Id.ToString(),
                before, new { b.Status, b.CancelledAt }, b.CancelReason);

            return await ToView(b);
        }

        // - lifecycle: Confirmed â†’ Completed / NoShow -----------------------

        public async Task<BookingView> MarkCompletedAsync(int bookingId, int actorId, string? note = null)
        {
            var b = await MustGetActive(bookingId);
            await AuthorizeStaffBranchAsync(b.BranchId, actorId);
            if (b.Status is not BookingStatus.Confirmed)
                throw new ConflictException($"Chi duoc danh dau hoan thanh don o trang thai Confirmed (hien tai: {b.Status}).");

            int claimed = await Uow.ExecuteSqlRawAsync(
                "UPDATE \"Bookings\" SET \"Status\" = 'Completed', \"PointsEarned\" = @p0, " +
                "  \"UpdatedAt\" = @p1 " +
                "WHERE \"Id\" = @p2 AND \"Status\" = 'Confirmed'",
                b.PayableAmount, DateTime.UtcNow, bookingId);
            if (claimed == 0)
            {
                var nowStatus = await Repo.GetByIdAsync(bookingId);
                throw new ConflictException(
                    $"Don {b.BookingCode} da o trang thai {nowStatus?.Status}, khong con the hoan thanh.");
            }
            b.Status = BookingStatus.Completed;
            b.PointsEarned = b.PayableAmount; // DEC-01: points bank at Completed
            b.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(note)) b.CancelReason = note; // reuse free-text field
            Repo.Update(b);
            await SaveAsync();

            if (b.PayableAmount > 0)
                await _points.AwardAsync(b.UserId, b.Id, b.PayableAmount,
                    $"completed booking {b.BookingCode}");

            await _audit.LogAsync(actorId, "booking.complete", "Booking", b.Id.ToString(),
                new { b.Status }, new { b.Status }, note);
            return await ToView(b);
        }

        public async Task<BookingView> MarkNoShowAsync(int bookingId, int actorId, string? note = null)
        {
            var b = await MustGetActive(bookingId);
            await AuthorizeStaffBranchAsync(b.BranchId, actorId);
            if (b.Status is not BookingStatus.Confirmed)
                throw new ConflictException($"Chi duoc gan co no-show don o trang thai Confirmed (hien tai: {b.Status}).");

            var slot = await Repository<TimeSlot>().GetByIdAsync(b.SlotId);
            if (slot is not null)
            {
                var slotStart = HanoiTime.SlotStart(b.Date, slot.StartTime);
                var hanoiNow = HanoiTime.HanoiNow();
                if (hanoiNow < slotStart)
                    throw new ConflictException(
                        $"Khung gio chua bat dau (bat dau {slot.StartTime:hh\\:mm}), khong the gan co no-show.");
            }

            int claimed = await Uow.ExecuteSqlRawAsync(
                "UPDATE \"Bookings\" SET \"Status\" = 'NoShow', \"CancelReason\" = @p0, " +
                "  \"CancelledAt\" = @p1, \"UpdatedAt\" = @p2 " +
                "WHERE \"Id\" = @p3 AND \"Status\" = 'Confirmed'",
                "No-show: khach khong den san" + (string.IsNullOrWhiteSpace(note) ? "" : " - " + note),
                DateTime.UtcNow, DateTime.UtcNow, bookingId);
            if (claimed == 0)
            {
                var nowStatus = await Repo.GetByIdAsync(bookingId);
                throw new ConflictException(
                    $"Don {b.BookingCode} da o trang thai {nowStatus?.Status}, khong con the gan co no-show.");
            }
            b.Status = BookingStatus.NoShow;
            b.CancelledAt = DateTime.UtcNow;
            b.CancelReason = "No-show: khach khong den san" + (string.IsNullOrWhiteSpace(note) ? "" : " - " + note);
            b.UpdatedAt = DateTime.UtcNow;
            Repo.Update(b);

            if (b.PointsEarned > 0 && await _points.HasEarnedRowAsync(b.Id))
                await _points.ClawbackAsync(b.UserId, b.Id, b.PointsEarned,
                    "no-show: " + b.BookingCode);

            // FEAT 1: notify the customer they were recorded as a no-show.
            await _notify.NotifyNoShowAsync(b.Id);

            // Increment the user's no-show counter (calendar month keyed).
            var user = await Repository<User>().GetByIdAsync(b.UserId);
            if (user is not null)
            {
                var monthStart = HanoiTime.HanoiToday().MonthStart();
                if (user.NoShowPeriod != monthStart)
                {
                    user.NoShowPeriod = monthStart;
                    user.NoShowCount = 0; // new month window
                }
                user.NoShowCount++;
                if (user.NoShowCount >= 3)
                {
                    // Block next Hanoi month: set the limit flag + period.
                    user.NoShowLimited = true;
                    user.NoShowLimitedPeriod = monthStart.AddMonths(1);
                }
                Repository<User>().Update(user);
            }

            await SaveAsync();
            await _audit.LogAsync(actorId, "booking.no-show", "Booking", b.Id.ToString(),
                new { beforeStatus = BookingStatus.Confirmed },
                new { afterStatus = b.Status }, note);
            return await ToView(b);
        }

        // - availability grid ----------------------------------------------

        public async Task<IReadOnlyList<CourtAvailabilityView>> GetAvailabilityAsync(int branchId, DateOnly date)
        {
            var courts = await Repository<Court>().GetByConditionAsync(
                c => c.BranchId == branchId && c.IsActive && c.DeletedAt == null);
            var slots = await Repository<TimeSlot>().GetByConditionAsync(
                s => s.IsActive && s.DeletedAt == null);
            var hanoiNowAvail = HanoiTime.HanoiNow();

            var courtIds = courts.Select(c => c.Id).ToList();
            var dateStatusRows = await Repository<CourtDateStatusEntry>().GetByConditionAsync(
                r => r.Date == date && courtIds.Contains(r.CourtId));
            var statusByCourt = dateStatusRows
                .ToDictionary(r => r.CourtId);

            var bookedRows = await Repo.GetByConditionAsync(b =>
                b.Date == date && courtIds.Contains(b.CourtId)
                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            var bookedByCourt = bookedRows
                .GroupBy(b => b.CourtId)
                .ToDictionary(g => g.Key, g => g.Select(b => b.SlotId).ToHashSet());

            var views = new List<CourtAvailabilityView>(courts.Count);
            foreach (var c in courts)
            {
                statusByCourt.TryGetValue(c.Id, out var schedule);
                var bookedSlots = bookedByCourt.TryGetValue(c.Id, out var set) ? set : default;
                bookedSlots ??= new HashSet<int>();

                var slotViews = slots.Select(sl =>
                {
                    var start = HanoiTime.SlotStart(date, sl.StartTime);
                    var dayStatus = schedule?.Blocks(sl.StartTime, sl.EndTime) == true ? schedule.Status : CourtDateStatus.Open;
                    var isLocked = dayStatus != CourtDateStatus.Open
                        || bookedSlots.Contains(sl.Id)
                        || start < hanoiNowAvail.AddMinutes(30);
                    var lockReason = isLocked
                        ? (dayStatus != CourtDateStatus.Open ? (dayStatus == CourtDateStatus.Maintenance ? "Bảo trì" : "Đóng")
                          : bookedSlots.Contains(sl.Id) ? "Đã đặt"
                          : "Đã qua giờ đặt")
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
            // Server-side COUNT â€” no materialising the bookings list.
            var u = userId; var g = groupId;
            return await Repo.CountByConditionAsync(b =>
                b.Date == date && b.UserId == u &&
                ((b.GroupId == null && g == null) || b.GroupId == g) &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
        }

        private async Task<CourtDateStatus> GetCourtDateStatus(int courtId, DateOnly date, TimeSlot slot)
        {
            var rows = await Repository<CourtDateStatusEntry>().GetByConditionAsync(
                r => r.CourtId == courtId && r.Date == date);
            var row = rows.FirstOrDefault();
            return row?.Blocks(slot.StartTime, slot.EndTime) == true ? row.Status : CourtDateStatus.Open;
        }

        private async Task AuthorizeView(Booking b, int requestingUserId)
        {
            if (b.UserId == requestingUserId) return;
            if (b.GroupId is { } gid && await _groups.CanBookForGroupAsync(gid, requestingUserId)) return;
            if (_currentUser.IsInRole("Admin") || _currentUser.IsInRole("Manager"))
            {
                await AuthorizeStaffBranchAsync(b.BranchId, requestingUserId);
                return;
            }
            throw new ConflictException("Khong co quyen xem dat cho nay.");
        }

        private async Task AuthorizeCancel(Booking b, int actorId)
        {
            if (b.UserId == actorId) return;
            if (b.GroupId is { } gid && await _groups.CanBookForGroupAsync(gid, actorId)) return;

            var ctx = _currentUser.Principal;
            if (ctx?.IsInRole("Admin") == true) return; // global
            if (ctx?.IsInRole("Manager") == true)
            {
                var actor = await Repository<User>().GetByIdAsync(actorId);
                if (actor is { Role: Role.Manager }
                    && actor.BranchId is { } ab && ab == b.BranchId)
                    return;
            }
            throw new ConflictException("Khong co quyen huy dat cho nay.");
        }

        private async Task<BookingView> ToView(Booking b)
        {
            var court = await Repository<Court>().GetByIdAsync(b.CourtId);
            var slot = await Repository<TimeSlot>().GetByIdAsync(b.SlotId);
            var user = await Repository<User>().GetByIdAsync(b.UserId);
            if (court is null || slot is null)
                throw new NotFoundException("Booking data", b.Id);

            var basePrice = b.TotalAmount; // Snapshot, inclusive of weekend surcharge.
            const int weekendOnly = 0; // A separate historical split was not persisted.
            var pointsVnd = b.PointsRedeemed * 20_000 / 1_000;
            var voucherVnd = b.DiscountAmount - pointsVnd;
            var breakdown = new BookingPriceBreakdown(
                basePrice, weekendOnly, voucherVnd, pointsVnd,
                b.TotalAmount, b.PayableAmount);

            return new BookingView(
                b.Id, b.BookingCode, b.CourtId, court.Name,
                b.Date.ToString("dd/MM/yyyy"),
                $"{slot.Label} ({slot.StartTime:hh\\:mm}-{slot.EndTime:hh\\:mm})",
                b.PlayerCount, b.Status.ToString(), b.TotalAmount, b.DiscountAmount,
                b.PayableAmount, b.PointsEarned, b.PointsRedeemed,
                b.GroupId, b.VoucherRedemptionId,
                b.CreatedAt, user?.FullName, breakdown, b.CancelReason, b.QrToken, b.CheckedInAt);
        }

        private static bool IsWeekend(DateOnly d) =>
            d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

        private static DateOnly ParseDate(string s) =>
            DateOnly.TryParse(s, out var d) ? d : throw new BadRequestException("Ngay khong hop le.");

        private async Task<Booking> MustGetActive(int bookingId)
        {
            var b = await Repo.GetByIdAsync(bookingId);
            if (b is null || b.DeletedAt.HasValue)
                throw new NotFoundException("Booking", bookingId);
            return b;
        }

        private static string GenerateCode() =>
            "BD-" + DateTime.UtcNow.ToString("yyMMdd") + "-" +
            Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }
}
