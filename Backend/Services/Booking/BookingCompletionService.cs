using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Points;
using Contract.Services.Operations;

namespace Services;

public sealed class BookingCompletionService(IUnitOfWork uow, IPointService points, IBookingTransitionGuard transitions)
{
    public async Task<Booking?> CompleteAsync(int bookingId, bool onlyIfDue = false)
    {
        Booking? result = null;
        await uow.RunInTransactionAsync(async () =>
        {
            await transitions.AcquireAsync(bookingId);
            if (onlyIfDue && await transitions.HasPendingRefundAsync(bookingId)) return;
            await transitions.EnsureNoPendingRefundAsync(bookingId);
            var repo = uow.GetRepository<Booking>();
            var booking = await repo.GetByIdAsync(bookingId)
                ?? throw new NotFoundException("Booking", bookingId);
            if (booking.DeletedAt.HasValue) throw new NotFoundException("Booking", bookingId);
            var slot = await uow.GetRepository<TimeSlot>().GetByIdAsync(booking.SlotId);
            if (slot is null || HanoiTime.SlotEnd(booking.Date, slot.EndTime) > HanoiTime.HanoiNow() || booking.CheckedInAt is null)
            {
                if (onlyIfDue) return;
                throw new ConflictException("Chỉ hoàn thành đơn đã check-in và kết thúc khung giờ chơi.");
            }
            if (await uow.Bookings.CompleteAsync(bookingId, 0, DateTime.UtcNow, HanoiTime.HanoiNow()) == 0)
            {
                if (onlyIfDue) return;
                throw new ConflictException("Chỉ hoàn thành đơn đã xác nhận và chưa được xử lý.");
            }
            await points.AwardAsync(booking.UserId, booking.Id, booking.PayableAmount,
                $"completed booking {booking.BookingCode}");
            var earned = (await uow.GetRepository<PointLedger>().GetByConditionAsync(p => p.LedgerKey == $"earn:{bookingId}"))
                .SingleOrDefault()?.Delta ?? 0;
            booking.Complete(earned, DateTime.UtcNow);
            repo.Update(booking);
            await uow.SaveChangesAsync();
            result = booking;
        });
        return result;
    }
}
