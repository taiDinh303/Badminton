using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Time;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class CoachCourtService(IUnitOfWork uow, IOperationsAccess access, IBusinessClock clock, IInboxService inbox, IBookingTransitionGuard transitions) : ICoachCourtService
{
    public async Task<bool> ReadyAsync(int? bookingId, DateTime start, DateTime end, int capacity, int branchId)
    {
        if (bookingId is null)
            return false;
        var b = (await uow.GetRepository<Booking>().GetByConditionAsync(x => x.Id == bookingId)).SingleOrDefault();
        if (b is null || b.DeletedAt.HasValue || b.Status != BookingStatus.Confirmed || b.BranchId != branchId || await transitions.HasPendingRefundAsync(b.Id))
            return false;
        var slot = await uow.GetRepository<TimeSlot>().GetByIdAsync(b.SlotId);
        var court = await uow.GetRepository<Court>().GetByIdAsync(b.CourtId);
        if (slot is null || court is null || court.DeletedAt.HasValue || !court.IsActive || capacity > court.Capacity)
            return false;
        var schedule = (await uow.GetRepository<CourtDateStatusEntry>().GetByConditionAsync(x => x.CourtId == b.CourtId && x.Date == b.Date)).SingleOrDefault();
        return (schedule is null || !schedule.Blocks(slot.StartTime, slot.EndTime)) && HanoiTime.SlotStart(b.Date, slot.StartTime).AddHours(-7) <= start && HanoiTime.SlotEnd(b.Date, slot.EndTime).AddHours(-7) >= end;
    }
    public async Task<IReadOnlyList<CoachCourtChoice>> ChoicesAsync(int branchId)
    {
        await access.StaffBranchAsync(branchId);
        var bookings = await uow.GetRepository<Booking>().GetByConditionAsync(x => x.BranchId == branchId && x.UserId == access.ActorId && x.GroupId == null && x.Status == BookingStatus.Confirmed && x.DeletedAt == null && x.Date >= clock.Today);
        var courts = (await uow.GetRepository<Court>().GetByConditionAsync(x => x.BranchId == branchId)).ToDictionary(x => x.Id);
        var slots = (await uow.GetRepository<TimeSlot>().GetAllAsync()).ToDictionary(x => x.Id);
        return bookings.OrderBy(x => x.Date).Take(200).Where(x => courts.ContainsKey(x.CourtId) && slots.ContainsKey(x.SlotId)).Select(x => new CoachCourtChoice(x.Id, x.BookingCode, courts[x.CourtId].Name, DateTime.SpecifyKind(HanoiTime.SlotStart(x.Date, slots[x.SlotId].StartTime).AddHours(-7), DateTimeKind.Utc), DateTime.SpecifyKind(HanoiTime.SlotEnd(x.Date, slots[x.SlotId].EndTime).AddHours(-7), DateTimeKind.Utc))).ToArray();
    }
    public Task AttachAsync(int slotId, int bookingId) => uow.RunInTransactionAsync(async () =>
    {
        await transitions.AcquireAsync(bookingId);
        var candidate = await uow.GetRepository<CoachSlot>().GetByIdAsync(slotId) ?? throw new NotFoundException("Lớp học", slotId);
        await uow.LockNotificationAsync($"coach:{candidate.CoachId}");
        var row = (await uow.GetRepository<CoachSlot>().GetFreshByIdAsync(slotId))!;
        var coach = await uow.GetRepository<CoachProfile>().GetByIdAsync(row.CoachId) ?? throw new NotFoundException("HLV", row.CoachId);
        await access.StaffBranchAsync(coach.BranchId);
        var booking = (await uow.GetRepository<Booking>().GetByConditionAsync(x => x.Id == bookingId)).SingleOrDefault();
        if (booking is null || booking.UserId != access.ActorId || booking.GroupId.HasValue)
            throw new ConflictException("Chỉ gắn đơn sân cá nhân do chính nhân viên thao tác đứng tên; không dùng đơn riêng của khách/nhóm cho lớp học.");
        if (row.Cancelled || row.StartsAtUtc <= clock.UtcNow)
            throw new ConflictException("Lớp đã hủy hoặc bắt đầu.");
        if (!await ReadyAsync(bookingId, row.StartsAtUtc, row.EndsAtUtc, row.Capacity, coach.BranchId))
            throw new ConflictException("Cần đơn sân đã xác nhận, đúng cơ sở, đủ sức chứa và bao trùm giờ học.");
        await uow.LockNotificationAsync($"coach-court:{bookingId}");
        if (await uow.GetRepository<CoachSlot>().CountByConditionAsync(x => x.Id != slotId && x.CourtBookingId == bookingId && !x.Cancelled && x.StartsAtUtc < row.EndsAtUtc && x.EndsAtUtc > row.StartsAtUtc) > 0)
            throw new ConflictException("Sân đã được gắn lớp khác trong giờ này.");
        row.CourtBookingId = bookingId;
        row.UpdatedAt = clock.UtcNow;
        uow.GetRepository<CoachSlot>().Update(row);
        await uow.SaveChangesAsync();
    });
    public async Task<int> ReconcileAsync()
    {
        var (rows, _) = await uow.GetRepository<CoachSlot>().GetPagedAsync(x => !x.Cancelled && x.EndsAtUtc > clock.UtcNow, x => x.LastCourtCheckedAtUtc ?? x.CreatedAt, OrderDirection.Asc, new(1, 100));
        var count = 0;
        foreach (var candidate in rows)
        {
            // Unbound drafts remain editable; only classes with a court or existing attendees need compensation.
            var coach = await uow.GetRepository<CoachProfile>().GetByIdAsync(candidate.CoachId);
            if (coach is null)
                continue;
            await uow.RunInTransactionAsync(async () =>
            {
                if (candidate.CourtBookingId is { } bookingId)
                    await transitions.AcquireAsync(bookingId);
                await uow.LockNotificationAsync($"coach:{candidate.CoachId}");
                var row = (await uow.GetRepository<CoachSlot>().GetFreshByIdAsync(candidate.Id))!;
                if (row.Cancelled)
                    return;
                row.LastCourtCheckedAtUtc = clock.UtcNow;
                uow.GetRepository<CoachSlot>().UpdateProperties(row, nameof(CoachSlot.LastCourtCheckedAtUtc));
                if (await ReadyAsync(row.CourtBookingId, row.StartsAtUtc, row.EndsAtUtc, row.Capacity, coach.BranchId) || (row.CourtBookingId is null && await uow.GetRepository<CoachingReservation>().CountByConditionAsync(x => x.SlotId == row.Id && x.Status == "Reserved") == 0))
                {
                    await uow.SaveChangesAsync();
                    return;
                }
                row.Cancelled = true;
                row.UpdatedAt = clock.UtcNow;
                uow.GetRepository<CoachSlot>().Update(row);
                var repo = uow.GetRepository<CoachingReservation>();
                foreach (var r in await repo.GetByConditionAsync(x => x.SlotId == row.Id && x.Status == "Reserved"))
                {
                    var reservation = (await repo.GetFreshByIdAsync(r.Id))!;
                    reservation.Status = "Cancelled";
                    reservation.UpdatedAt = clock.UtcNow;
                    repo.Update(reservation);
                    await inbox.PublishAsync(r.UserId, null, $"coach-court-cancelled:{row.Id}", "Buổi học thay đổi do lịch sân", "Đơn sân không còn hợp lệ với lớp học. Chỗ học đã hủy; liên hệ cơ sở để xếp lớp khác và đối chiếu khoản đã đóng tại quầy.");
                }
                await uow.SaveChangesAsync();
                count++;
            });
        }
        return count;
    }
}
