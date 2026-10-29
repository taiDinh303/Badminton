using Badminton.Core.Base;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using Contract.Services.Maintenance;

namespace Services.Operations;

public sealed class BookingTransitionGuard(IUnitOfWork uow) : IBookingTransitionGuard
{
    public async Task EnsureMaintenanceScopeAsync(Booking booking, MaintenanceBookingScope scope)
    {
        if (booking.CourtId != scope.CourtId || booking.Date != scope.Date || booking.SlotId != scope.SlotId || booking.DeletedAt.HasValue)
            throw new MaintenanceScopeChangedException();
        // The booking transition lock is acquired first by the caller. Keep the closure
        // stable through the claim/intent commit, including a concurrent reopening.
        await uow.LockCourtDateAsync(scope.CourtId, scope.Date);
        var schedule = (await uow.GetRepository<CourtDateStatusEntry>().GetByConditionAsync(s => s.CourtId == scope.CourtId && s.Date == scope.Date)).SingleOrDefault();
        var slot = (await uow.GetRepository<TimeSlot>().GetByConditionAsync(s => s.Id == scope.SlotId)).SingleOrDefault();
        if (schedule is null || slot is null || !schedule.Blocks(slot.StartTime, slot.EndTime))
            throw new MaintenanceScopeChangedException();
    }

    public Task AcquireAsync(int bookingId) => uow.LockBookingRequestAsync($"booking-transition:{bookingId}");

    public async Task<bool> HasPendingRefundAsync(int bookingId) =>
        await uow.GetRepository<OnlineRefundOperation>().CountByConditionAsync(o =>
            o.BookingId == bookingId && !o.Succeeded && !o.IsLateReceipt) > 0;

    public async Task EnsureNoPendingRefundAsync(int bookingId)
    {
        if (await HasPendingRefundAsync(bookingId))
            throw new ConflictException("Đơn đang chờ xác minh hoàn tiền. Cần đối soát trước khi thay đổi hoặc sử dụng lịch.");
    }
}
