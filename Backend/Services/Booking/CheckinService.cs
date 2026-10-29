using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Booking;
using Contract.Services.Operations;
using Badminton.Core.Base;
using Badminton.Core.Enums;

namespace Services
{
    public sealed class CheckinService : BaseService, ICheckinService
    {
        private readonly IBookingTransitionGuard _transitions;
        public CheckinService(IUnitOfWork uow,
            Badminton.Core.Security.ICurrentUser currentUser, IBookingTransitionGuard transitions)
            : base(uow, currentUser) { _transitions = transitions; }

        private IGenericRepository<Booking> Bookings => Repository<Booking>();

        public async Task<Booking> CheckinByTokenAsync(string qrToken, int actorId)
        {
            if (string.IsNullOrWhiteSpace(qrToken))
                throw new BadRequestException("Thiếu mã QR.");
            var rows = await Bookings.GetByConditionAsync(b => b.QrToken == qrToken);
            if (rows.Count == 0)
                throw new NotFoundException("QR", qrToken);
            await AuthorizeStaffBranchAsync(rows.First().BranchId, actorId);
            return await ApplyCheckinAsync(rows.First());
        }

        public async Task<Booking> CheckinByCodeAsync(string bookingCode, int actorId)
        {
            if (string.IsNullOrWhiteSpace(bookingCode))
                throw new BadRequestException("Thiếu mã đơn.");
            var rows = await Bookings.GetByConditionAsync(b => b.BookingCode == bookingCode);
            if (rows.Count == 0)
                throw new NotFoundException("Booking", bookingCode);
            await AuthorizeStaffBranchAsync(rows.First().BranchId, actorId);
            return await ApplyCheckinAsync(rows.First());
        }

        private async Task<Booking> ApplyCheckinAsync(Booking b)
        {
            Booking result = null!;
            await Uow.RunInTransactionAsync(async () =>
            {
                await _transitions.AcquireAsync(b.Id);
                await _transitions.EnsureNoPendingRefundAsync(b.Id);
                result = await ApplyCheckinCoreAsync(b);
            });
            return result;
        }

        private async Task<Booking> ApplyCheckinCoreAsync(Booking b)
        {
            if (b.DeletedAt.HasValue) throw new NotFoundException("Booking", b.Id);
            if (b.Status == BookingStatus.Pending && (b.PendingExpiresAt is null || b.PendingExpiresAt <= DateTime.UtcNow))
                throw new ConflictException("Đơn đã hết thời gian giữ chỗ, không thể check-in.");
            if (b.Status is not (BookingStatus.Confirmed or BookingStatus.Pending))
                throw new ConflictException(
                    $"Đơn {b.BookingCode} ở trạng thái {b.Status}, không thể check-in.");

            // Idempotent: already checked in → return as-is.
            if (b.CheckedInAt is { } already)
                return b;

            var slot = await Repository<TimeSlot>().GetByIdAsync(b.SlotId)
                ?? throw new NotFoundException("Slot", b.SlotId);
            var businessNow = Badminton.Core.Utils.HanoiTime.HanoiNow();
            if (businessNow < Badminton.Core.Utils.HanoiTime.SlotStart(b.Date, slot.StartTime).AddMinutes(-30)
                || businessNow > Badminton.Core.Utils.HanoiTime.SlotEnd(b.Date, slot.EndTime))
                throw new ConflictException("Chỉ check-in từ 30 phút trước giờ bắt đầu đến hết khung giờ chơi.");

            var now = DateTime.UtcNow;
            int claimed = await Uow.Bookings.CheckinAsync(b.Id, now, businessNow, b.BranchId);
            if (claimed == 0)
                throw new ConflictException(
                    $"Đơn {b.BookingCode} vừa được check-in bởi thao tác khác.");

            b.CheckedInAt = now;
            b.UpdatedAt = now;
            return b;
        }

        public async Task<IReadOnlyList<Booking>> GetUnconfirmedForSlotAsync(
            int courtId, DateOnly date, string slotLabel)
        {
            var court = await Repository<Court>().GetByIdAsync(courtId)
                ?? throw new NotFoundException("Court", courtId);
            await AuthorizeStaffBranchAsync(court.BranchId, _currentUser.Id);
            var slots = await Repository<TimeSlot>()
                .GetByConditionAsync(s => s.Label == slotLabel);
            if (slots.Count == 0)
                throw new NotFoundException("Slot", slotLabel);
            var slotId = slots.First().Id;

            // All bookings for this slot that have not been checked in yet.
            var rows = await Bookings.GetByConditionAsync(b =>
                b.CourtId == courtId && b.Date == date && b.SlotId == slotId
                && b.CheckedInAt == null
                && (b.Status == BookingStatus.Confirmed
                    || b.Status == BookingStatus.Pending));
            return rows.ToList();
        }
    }
}
