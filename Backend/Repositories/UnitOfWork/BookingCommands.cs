using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UnitOfWork;

internal sealed class BookingCommands(BadmintonBookingDbContext db) : IBookingCommands
{
    public Task<int> RescheduleAsync(int id, int oldCourtId, int oldSlotId, DateOnly oldDate,
        int courtId, int slotId, DateOnly date, int branchId, DateTime now, DateTime businessNow)
    {
        var day = DateOnly.FromDateTime(businessNow);
        var time = TimeOnly.FromDateTime(businessNow);
        return db.Bookings.Where(b => b.Id == id && b.DeletedAt == null && b.CheckedInAt == null
            && !db.Set<OnlineRefundOperation>().Any(o => o.BookingId == b.Id && !o.Succeeded && !o.IsLateReceipt)
            && b.CourtId == oldCourtId && b.SlotId == oldSlotId && b.Date == oldDate
            && (b.Status == BookingStatus.Confirmed || (b.Status == BookingStatus.Pending && b.PendingExpiresAt > now))
            && b.Slot != null && (b.Date > day || (b.Date == day && b.Slot.StartTime > time)))
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.CourtId, courtId).SetProperty(b => b.SlotId, slotId)
                .SetProperty(b => b.Date, date).SetProperty(b => b.BranchId, branchId).SetProperty(b => b.UpdatedAt, now));
    }
    public Task<int> ConfirmAsync(int id, string qrToken, DateTime now, int? expectedBranchId = null) => db.Bookings
        .Where(b => b.Id == id && b.DeletedAt == null && b.Status == BookingStatus.Pending
            && b.PendingExpiresAt != null && b.PendingExpiresAt > now && (expectedBranchId == null || b.BranchId == expectedBranchId))
        .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Confirmed)
            .SetProperty(b => b.QrToken, qrToken).SetProperty(b => b.UpdatedAt, now));

    public Task<int> CompleteAsync(int id, int points, DateTime now, DateTime? businessNow = null)
    {
        var local = businessNow ?? now.AddHours(7);
        var day = DateOnly.FromDateTime(local);
        var time = TimeOnly.FromDateTime(local);
        return db.Bookings
        .Where(b => b.Id == id && b.DeletedAt == null && b.Status == BookingStatus.Confirmed && b.CheckedInAt != null)
        .Where(b => !db.Set<OnlineRefundOperation>().Any(o => o.BookingId == b.Id && !o.Succeeded && !o.IsLateReceipt))
        .Where(b => b.Slot != null && (b.Date < day || (b.Date == day && b.Slot.EndTime <= time)))
        .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Completed)
            .SetProperty(b => b.PointsEarned, points).SetProperty(b => b.UpdatedAt, now));
    }

    public Task<int> CancelPendingAsync(int id, string reason, DateTime now, int? expectedBranchId = null) => db.Bookings
        .Where(b => b.Id == id && b.Status == BookingStatus.Pending && (expectedBranchId == null || b.BranchId == expectedBranchId))
        .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Cancelled)
            .SetProperty(b => b.CancelReason, reason).SetProperty(b => b.CancelledAt, now)
            .SetProperty(b => b.UpdatedAt, now));

    public Task<int> MarkNoShowAsync(int id, string reason, DateTime now, DateTime? businessNow = null, int? expectedBranchId = null)
    {
        var local = businessNow ?? now.AddHours(7);
        var day = DateOnly.FromDateTime(local);
        var time = TimeOnly.FromDateTime(local);
        return db.Bookings
        .Where(b => b.Id == id && b.DeletedAt == null && b.Status == BookingStatus.Confirmed && b.CheckedInAt == null && (expectedBranchId == null || b.BranchId == expectedBranchId))
        .Where(b => !db.Set<OnlineRefundOperation>().Any(o => o.BookingId == b.Id && !o.Succeeded && !o.IsLateReceipt))
        .Where(b => b.Slot != null && (b.Date < day || (b.Date == day && b.Slot.StartTime <= time)))
        .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.NoShow)
            .SetProperty(b => b.CancelReason, reason).SetProperty(b => b.CancelledAt, now)
            .SetProperty(b => b.UpdatedAt, now));
    }

    public Task<int> ClaimRefundAsync(int id, bool cancel, string reason, DateTime now, int? expectedBranchId = null)
    {
        var rows = db.Bookings.Where(b => b.Id == id && b.DeletedAt == null
            && (expectedBranchId == null || b.BranchId == expectedBranchId) &&
            (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed));
        return cancel
            ? rows.ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Cancelled)
                .SetProperty(b => b.CancelReason, reason).SetProperty(b => b.CancelledAt, now)
                .SetProperty(b => b.UpdatedAt, now))
            : rows.ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Refunded)
                .SetProperty(b => b.CancelReason, reason).SetProperty(b => b.UpdatedAt, now));
    }

    public Task<int> CheckinAsync(int id, DateTime now, DateTime? businessNow = null, int? expectedBranchId = null)
    {
        var local = businessNow ?? now.AddHours(7);
        var day = DateOnly.FromDateTime(local);
        var time = TimeOnly.FromDateTime(local);
        var early = local.AddMinutes(30);
        var earlyDay = DateOnly.FromDateTime(early);
        var earlyTime = TimeOnly.FromDateTime(early);
        return db.Bookings
        .Where(b => !db.Set<OnlineRefundOperation>().Any(o => o.BookingId == b.Id && !o.Succeeded && !o.IsLateReceipt))
        .Where(b => b.Id == id && b.DeletedAt == null && b.CheckedInAt == null && (expectedBranchId == null || b.BranchId == expectedBranchId) &&
            (b.Status == BookingStatus.Confirmed || (b.Status == BookingStatus.Pending && b.PendingExpiresAt > now)))
        .Where(b => b.Slot != null && (b.Date < earlyDay || (b.Date == earlyDay && b.Slot.StartTime <= earlyTime))
            && (b.Date > day || (b.Date == day && b.Slot.EndTime >= time)))
        .ExecuteUpdateAsync(s => s.SetProperty(b => b.CheckedInAt, now).SetProperty(b => b.UpdatedAt, now));
    }
}
