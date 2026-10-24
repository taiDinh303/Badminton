using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Points;

namespace Services;

public sealed class BookingCompletionService(IUnitOfWork uow, IPointService points)
{
    public async Task<Booking?> CompleteAsync(int bookingId, bool onlyIfDue = false)
    {
        Booking? result = null;
        await uow.RunInTransactionAsync(async () =>
        {
            var repo = uow.GetRepository<Booking>();
            var booking = await repo.GetByIdAsync(bookingId)
                ?? throw new NotFoundException("Booking", bookingId);
            if (booking.DeletedAt.HasValue) throw new NotFoundException("Booking", bookingId);
            if (onlyIfDue)
            {
                var slot = await uow.GetRepository<TimeSlot>().GetByIdAsync(booking.SlotId);
                if (slot is null || HanoiTime.SlotEnd(booking.Date, slot.EndTime) > HanoiTime.HanoiNow()) return;
            }
            if (await uow.Bookings.CompleteAsync(bookingId, 0, DateTime.UtcNow) == 0)
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
