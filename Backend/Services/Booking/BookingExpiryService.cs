using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Points;
using Contract.Services.Voucher;

namespace Services;

public sealed class BookingExpiryService(IUnitOfWork uow, IPointService points, IVoucherService vouchers, Contract.Services.Notify.INotifyService notify)
{
    public async Task<bool> ExpireAsync(int bookingId, DateTime now)
    {
        var expired = false;
        await uow.RunInTransactionAsync(async () =>
        {
            var booking = await uow.GetRepository<Booking>().GetByIdAsync(bookingId);
            if (booking is null || booking.Status != BookingStatus.Pending ||
                booking.PendingExpiresAt is not { } expiry || expiry > now) return;
            if (await uow.Bookings.CancelPendingAsync(bookingId, "Hết hạn thanh toán (10 phút)", now) == 0) return;
            if (booking.VoucherRedemptionId is { } id) await vouchers.ReleaseVoucherAsync(id);
            if (booking.PointsRedeemed > 0)
                await points.RestoreAsync(booking.UserId, booking.Id, booking.PointsRedeemed, booking.BookingCode);
            await notify.QueueBookingEventAsync(NotifyEvent.BookingExpired, bookingId);
            await uow.SaveChangesAsync();
            expired = true;
        });
        return expired;
    }
}
