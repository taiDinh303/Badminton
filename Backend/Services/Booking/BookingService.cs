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
        private readonly BookingCreationService _creation;
        private readonly BookingQuoteService _quote;
        private readonly BookingAvailabilityService _availability;
        private readonly IVoucherService _vouchers;
        private readonly IGroupService _groups;
        private readonly IAuditService _audit;
        private readonly IPointService _points;
        private readonly IPaymentService _payments;
        private readonly BookingLifecycleService _lifecycle;

        public BookingService(IUnitOfWork uow, ICurrentUser currentUser,
            BookingCreationService creation, IVoucherService vouchers, IGroupService groups,
            IAuditService audit, IPointService points, IPaymentService payments,
            BookingLifecycleService lifecycle, BookingAvailabilityService availability, BookingQuoteService quote)
            : base(uow, currentUser)
        {
            _creation = creation;
            _quote = quote;
            _availability = availability;
            _vouchers = vouchers;
            _groups = groups;
            _audit = audit;
            _points = points;
            _payments = payments;
            _lifecycle = lifecycle;
        }

        private IGenericRepository<Booking> Repo => Repository<Booking>();

        public Task<BookingPriceBreakdown> QuoteAsync(CreateBookingRequest request, int userId) => _quote.QuoteAsync(request, userId);

        public async Task<BookingView> CreateAsync(CreateBookingRequest request, int userId) =>
            await ToView(await _creation.CreateAsync(request, userId));

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
            GetAllPagedAsync(string? status, int? branchId, Page page, string? search = null, DateOnly? date = null, bool needsAttendance = false)
        {
            var bi = await ResolveStaffBranchAsync(branchId);
            bool hasStatus = !string.IsNullOrWhiteSpace(status);
            BookingStatus? stEnum = hasStatus
                && Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var p)
                ? p
                : null;
            if (hasStatus && stEnum is null)
                throw new BadRequestException($"Trang thai '{status}' khong hop le.");

            var term = search?.Trim().ToLowerInvariant();
            var today = HanoiTime.HanoiToday(); var now = TimeOnly.FromDateTime(HanoiTime.HanoiNow());
            if (term?.Length > 100) throw new BadRequestException("Từ khóa tối đa 100 ký tự.");
            System.Linq.Expressions.Expression<Func<Booking, bool>> predicate = b =>
                b.DeletedAt == null
                && (stEnum == null || b.Status == stEnum!.Value)
                && (bi == null || b.BranchId == bi)
                && (date == null || b.Date == date)
                && (!needsAttendance || (b.Status == BookingStatus.Confirmed && b.CheckedInAt == null && b.Slot != null
                    && (b.Date < today || (b.Date == today && b.Slot.EndTime <= now))))
                && (term == null || term == "" || b.BookingCode.ToLower().Contains(term)
                    || (b.User != null && (b.User.FullName.ToLower().Contains(term) || b.User.PhoneNumber.Contains(term)))
                    || (b.Court != null && b.Court.Name.ToLower().Contains(term)));
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
                var pointsVnd = checked((int)((long)b.PointsRedeemed * 20_000 / 1_000));
                var voucherVnd = b.DiscountAmount - pointsVnd;
                var breakdown = new BookingPriceBreakdown(
                    basePrice, weekendOnly, voucherVnd, pointsVnd,
                    b.TotalAmount, b.PayableAmount);
                users.TryGetValue(b.UserId, out var user);

                views.Add(new BookingView(
                    b.Id, b.BookingCode, b.CourtId, court.Name,
                    b.Date.ToString("dd/MM/yyyy"),
                    $"{slot.Label} ({slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm})",
                    b.PlayerCount, b.Status.ToString(), b.TotalAmount, b.DiscountAmount,
                    b.PayableAmount, b.PointsEarned, b.PointsRedeemed,
                    b.GroupId, b.VoucherRedemptionId,
                    b.CreatedAt, user?.FullName, breakdown, b.CancelReason, b.QrToken, b.CheckedInAt, b.UserId, b.PendingExpiresAt, DateTime.UtcNow,
                    DateTime.SpecifyKind(HanoiTime.SlotStart(b.Date, slot.StartTime).Add(-HanoiTime.FixedOffset), DateTimeKind.Utc),
                    DateTime.SpecifyKind(HanoiTime.SlotEnd(b.Date, slot.EndTime).Add(-HanoiTime.FixedOffset), DateTimeKind.Utc)));
            }
            return views;
        }

        // - cancel (P0-1: paid orders go through a tiered refund) ----------

        public async Task CancelForMaintenanceAsync(int bookingId, int actorId)
        {
            var booking = await Repo.GetByIdAsync(bookingId) ?? throw new NotFoundException("Booking", bookingId);
            await AuthorizeStaffBranchAsync(booking.BranchId, actorId);
            if (booking.Status == BookingStatus.Confirmed)
            {
                await _payments.RefundForMaintenanceAsync(bookingId, actorId, "Hoàn toàn phần do sân bảo trì/đóng");
                return;
            }
            await Uow.RunInTransactionAsync(() => CancelForMaintenanceCoreAsync(bookingId, actorId));
        }

        private async Task CancelForMaintenanceCoreAsync(int bookingId, int actorId)
        {
            var b = await Repo.GetByIdAsync(bookingId) ?? throw new NotFoundException("Booking", bookingId);
            await AuthorizeStaffBranchAsync(b.BranchId, actorId);
            if (b.Status == BookingStatus.Confirmed)
            {
                throw new ConflictException("Đơn vừa được thanh toán. Lưu lại lịch để xử lý hoàn tiền.");
            }
            if (b.Status != BookingStatus.Pending) return;
            var now = DateTime.UtcNow;
            var claimed = await Uow.Bookings.CancelPendingAsync(bookingId, "Sân bảo trì/đóng", now);
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
            var booking = await Repo.GetByIdAsync(bookingId) ?? throw new NotFoundException("Booking", bookingId);
            if (booking.DeletedAt.HasValue) throw new NotFoundException("Booking", bookingId);
            await AuthorizeCancel(booking, actorId);
            if (booking.Status is BookingStatus.Confirmed or BookingStatus.Completed)
            {
                await _payments.CancelRefundAsync(bookingId, actorId, "Khach/manager huy don da thanh toan.");
                return await ToView((await Repo.GetByConditionAsync(x => x.Id == bookingId)).Single());
            }
            BookingView result = null!;
            await Uow.RunInTransactionAsync(async () => result = await CancelCoreAsync(bookingId, actorId));
            return result;
        }

        private async Task<BookingView> CancelCoreAsync(int bookingId, int actorId)
        {
            var b = await Repo.GetByIdAsync(bookingId);
            if (b is null || b.DeletedAt.HasValue)
                throw new NotFoundException("Booking", bookingId);
            await AuthorizeCancel(b, actorId);

            if (b.Status is BookingStatus.Cancelled or BookingStatus.NoShow or BookingStatus.Refunded)
                throw new ConflictException($"Dat cho da o trang thai {b.Status}.");

            if (b.Status is BookingStatus.Confirmed or BookingStatus.Completed)
            {
                throw new ConflictException("Đơn vừa được thanh toán. Vui lòng gửi lại yêu cầu hủy để xử lý hoàn tiền.");
            }

            // Unpaid (Pending) path: just release the slot + voucher + points.
            var redemption = b.VoucherRedemptionId is { } rid
                ? await Repository<VoucherRedemption>().GetByIdAsync(rid)
                : null;

            var before = new { b.Status, b.CancelReason };
            if (await Uow.Bookings.CancelPendingAsync(bookingId, "Khach/manager huy", DateTime.UtcNow) == 0)
                throw new ConflictException("Đơn vừa thay đổi trạng thái. Vui lòng tải lại.");
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

        public async Task<BookingView> MarkCompletedAsync(int bookingId, int actorId, string? note = null) =>
            await ToView(await _lifecycle.MarkCompletedAsync(bookingId, actorId, note));

        public async Task<BookingView> MarkNoShowAsync(int bookingId, int actorId, string? note = null) =>
            await ToView(await _lifecycle.MarkNoShowAsync(bookingId, actorId, note));

        // - availability grid ----------------------------------------------

        public Task<IReadOnlyList<CourtAvailabilityView>> GetAvailabilityAsync(int branchId, DateOnly date) =>
            _availability.GetAsync(branchId, date);

        // - helpers ---------------------------------------------------------

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
            var pointsVnd = checked((int)((long)b.PointsRedeemed * 20_000 / 1_000));
            var voucherVnd = b.DiscountAmount - pointsVnd;
            var breakdown = new BookingPriceBreakdown(
                basePrice, weekendOnly, voucherVnd, pointsVnd,
                b.TotalAmount, b.PayableAmount);

            return new BookingView(
                b.Id, b.BookingCode, b.CourtId, court.Name,
                b.Date.ToString("dd/MM/yyyy"),
                $"{slot.Label} ({slot.StartTime:HH\\:mm}-{slot.EndTime:HH\\:mm})",
                b.PlayerCount, b.Status.ToString(), b.TotalAmount, b.DiscountAmount,
                b.PayableAmount, b.PointsEarned, b.PointsRedeemed,
                b.GroupId, b.VoucherRedemptionId,
                b.CreatedAt, user?.FullName, breakdown, b.CancelReason, b.QrToken, b.CheckedInAt, b.UserId, b.PendingExpiresAt, DateTime.UtcNow,
                    DateTime.SpecifyKind(HanoiTime.SlotStart(b.Date, slot.StartTime).Add(-HanoiTime.FixedOffset), DateTimeKind.Utc),
                    DateTime.SpecifyKind(HanoiTime.SlotEnd(b.Date, slot.EndTime).Add(-HanoiTime.FixedOffset), DateTimeKind.Utc));
        }

        private async Task<Booking> MustGetActive(int bookingId)
        {
            var b = await Repo.GetByIdAsync(bookingId);
            if (b is null || b.DeletedAt.HasValue)
                throw new NotFoundException("Booking", bookingId);
            return b;
        }

    }
}
