using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Security;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Notify;
using Contract.Services.Points;

namespace Services;

public sealed class BookingLifecycleService(IUnitOfWork uow, ICurrentUser currentUser,
    IPointService points, IAuditService audit, INotifyService notify, BookingCompletionService completion) : BaseService(uow, currentUser)
{
    private readonly IPointService _points = points;
    private readonly BookingCompletionService _completion = completion;
    private readonly IAuditService _audit = audit;
    private readonly INotifyService _notify = notify;
    private IGenericRepository<Booking> Repo => Repository<Booking>();

    public async Task<Booking> MarkCompletedAsync(int bookingId, int actorId, string? note = null)
    {
        Booking result = null!;
        await Uow.RunInTransactionAsync(async () => result = await MarkCompletedCoreAsync(bookingId, actorId, note));
        return result;
    }

    private async Task<Booking> MarkCompletedCoreAsync(int bookingId, int actorId, string? note = null)
    {
        var b = await MustGetActive(bookingId);
        await AuthorizeStaffBranchAsync(b.BranchId, actorId);
        if (b.Status is not BookingStatus.Confirmed)
            throw new ConflictException($"Chi duoc danh dau hoan thanh don o trang thai Confirmed (hien tai: {b.Status}).");

        b = (await _completion.CompleteAsync(bookingId))!;
        if (!string.IsNullOrWhiteSpace(note)) b.CancelReason = note;
        await SaveAsync();

        await _audit.LogAsync(actorId, "booking.complete", "Booking", b.Id.ToString(),
            new { Status = BookingStatus.Confirmed }, new { b.Status }, note);
        return b;
    }

    public async Task<Booking> MarkNoShowAsync(int bookingId, int actorId, string? note = null)
    {
        Booking result = null!;
        await Uow.RunInTransactionAsync(async () => result = await MarkNoShowCoreAsync(bookingId, actorId, note));
        await _notify.NotifyNoShowAsync(result.Id);
        return result;
    }

    private async Task<Booking> MarkNoShowCoreAsync(int bookingId, int actorId, string? note = null)
    {
        var b = await MustGetActive(bookingId);
        await AuthorizeStaffBranchAsync(b.BranchId, actorId);
        if (b.Status is not BookingStatus.Confirmed)
            throw new ConflictException($"Chi duoc gan co no-show don o trang thai Confirmed (hien tai: {b.Status}).");
        if (b.CheckedInAt.HasValue)
            throw new ConflictException("Khách đã check-in, không thể ghi nhận vắng mặt.");

        var slot = await Repository<TimeSlot>().GetByIdAsync(b.SlotId);
        if (slot is not null)
        {
            var slotStart = HanoiTime.SlotStart(b.Date, slot.StartTime);
            var hanoiNow = HanoiTime.HanoiNow();
            if (hanoiNow < slotStart)
                throw new ConflictException(
                    $"Khung gio chua bat dau (bat dau {slot.StartTime:HH\\:mm}), khong the gan co no-show.");
        }

        int claimed = await Uow.Bookings.MarkNoShowAsync(bookingId,
            "No-show: khach khong den san" + (string.IsNullOrWhiteSpace(note) ? "" : " - " + note), DateTime.UtcNow, HanoiTime.HanoiNow());
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
            await _points.ClawbackAsync(b.UserId, b.Id, b.PayableAmount,
                "no-show: " + b.BookingCode);

        // Serialize counters for different bookings of the same customer.
        await Uow.LockPointsUserAsync(b.UserId);
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
            Repository<User>().UpdateProperties(user, nameof(User.NoShowPeriod), nameof(User.NoShowCount),
                nameof(User.NoShowLimited), nameof(User.NoShowLimitedPeriod));
        }

        await SaveAsync();
        await _notify.QueueBookingEventAsync(NotifyEvent.NoShowRecorded, b.Id);
        await _audit.LogAsync(actorId, "booking.no-show", "Booking", b.Id.ToString(),
            new { beforeStatus = BookingStatus.Confirmed },
            new { afterStatus = b.Status }, note);
        return b;
    }

    private async Task<Booking> MustGetActive(int bookingId)
    {
        var b = await Repo.GetByIdAsync(bookingId);
        if (b is null || b.DeletedAt.HasValue)
            throw new NotFoundException("Booking", bookingId);
        return b;
    }

}
