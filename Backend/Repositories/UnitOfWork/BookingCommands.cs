using Badminton.Core.Enums;
using Contract.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UnitOfWork;

internal sealed class BookingCommands(BadmintonBookingDbContext db) : IBookingCommands
{
    public Task<int> ConfirmAsync(int id, string qrToken, DateTime now) => db.Bookings
        .Where(b => b.Id == id && b.DeletedAt == null && b.Status == BookingStatus.Pending
            && b.PendingExpiresAt != null && b.PendingExpiresAt > now)
        .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Confirmed)
            .SetProperty(b => b.QrToken, qrToken).SetProperty(b => b.UpdatedAt, now));

    public Task<int> CompleteAsync(int id, int points, DateTime now, DateTime? businessNow = null)
    {
        var local = businessNow ?? now.AddHours(7);
        var day = DateOnly.FromDateTime(local); var time = TimeOnly.FromDateTime(local);
        return db.Bookings
        .Where(b => b.Id == id && b.DeletedAt == null && b.Status == BookingStatus.Confirmed && b.CheckedInAt != null)
        .Where(b => b.Slot != null && (b.Date < day || (b.Date == day && b.Slot.EndTime <= time)))
        .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Completed)
            .SetProperty(b => b.PointsEarned, points).SetProperty(b => b.UpdatedAt, now));
    }

    public Task<int> CancelPendingAsync(int id, string reason, DateTime now) => db.Bookings
        .Where(b => b.Id == id && b.Status == BookingStatus.Pending)
        .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Cancelled)
            .SetProperty(b => b.CancelReason, reason).SetProperty(b => b.CancelledAt, now)
            .SetProperty(b => b.UpdatedAt, now));

    public Task<int> MarkNoShowAsync(int id, string reason, DateTime now, DateTime? businessNow = null)
    {
        var local = businessNow ?? now.AddHours(7);
        var day = DateOnly.FromDateTime(local); var time = TimeOnly.FromDateTime(local);
        return db.Bookings
        .Where(b => b.Id == id && b.DeletedAt == null && b.Status == BookingStatus.Confirmed && b.CheckedInAt == null)
        .Where(b => b.Slot != null && (b.Date < day || (b.Date == day && b.Slot.StartTime <= time)))
        .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.NoShow)
            .SetProperty(b => b.CancelReason, reason).SetProperty(b => b.CancelledAt, now)
            .SetProperty(b => b.UpdatedAt, now));
    }

    public Task<int> ClaimRefundAsync(int id, bool cancel, string reason, DateTime now)
    {
        var rows = db.Bookings.Where(b => b.Id == id &&
            (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed));
        return cancel
            ? rows.ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Cancelled)
                .SetProperty(b => b.CancelReason, reason).SetProperty(b => b.CancelledAt, now)
                .SetProperty(b => b.UpdatedAt, now))
            : rows.ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Refunded)
                .SetProperty(b => b.CancelReason, reason).SetProperty(b => b.UpdatedAt, now));
    }

    public Task<int> CheckinAsync(int id, DateTime now, DateTime? businessNow = null)
    {
        var local = businessNow ?? now.AddHours(7);
        var day = DateOnly.FromDateTime(local); var time = TimeOnly.FromDateTime(local);
        var early = local.AddMinutes(30);
        var earlyDay = DateOnly.FromDateTime(early); var earlyTime = TimeOnly.FromDateTime(early);
        return db.Bookings
        .Where(b => b.Id == id && b.DeletedAt == null && b.CheckedInAt == null &&
            (b.Status == BookingStatus.Confirmed || (b.Status == BookingStatus.Pending && b.PendingExpiresAt > now)))
        .Where(b => b.Slot != null && (b.Date < earlyDay || (b.Date == earlyDay && b.Slot.StartTime <= earlyTime))
            && (b.Date > day || (b.Date == day && b.Slot.EndTime >= time)))
        .ExecuteUpdateAsync(s => s.SetProperty(b => b.CheckedInAt, now).SetProperty(b => b.UpdatedAt, now));
    }
}
