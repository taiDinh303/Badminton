using Badminton.Core.Base;
using Badminton.Core.Time;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class GroupAttendanceService(IUnitOfWork uow, IOperationsAccess access, IInboxService inbox, IBusinessClock clock) : IGroupAttendanceService
{
    public async Task<IReadOnlyList<GroupMeetingView>> MeetingsAsync(int groupId)
    {
        await access.ActorAsync();
        var group = await uow.GetRepository<PlayGroup>().GetByIdAsync(groupId);
        if (group is null || group.DeletedAt.HasValue || await uow.GetRepository<GroupMember>().CountByConditionAsync(m => m.GroupId == groupId && m.UserId == access.ActorId) == 0)
            throw new ConflictException("Chỉ thành viên hiện tại được xem lịch nhóm.");
        var rows = await uow.GetRepository<Booking>().GetByConditionAsync(b => b.DeletedAt == null && b.GroupId == groupId && (b.Status == Badminton.Core.Enums.BookingStatus.Pending || b.Status == Badminton.Core.Enums.BookingStatus.Confirmed));
        var slots = (await uow.GetRepository<TimeSlot>().GetAllAsync()).ToDictionary(s => s.Id);
        return rows.OrderBy(b => b.Date).ThenBy(b => b.SlotId).Select(b => new GroupMeetingView(b.Id, b.BookingCode, b.Date.ToString("yyyy-MM-dd"), slots.GetValueOrDefault(b.SlotId)?.Label ?? "", b.UserId, b.PlayerCount)).ToArray();
    }
    public async Task<IReadOnlyList<RsvpView>> RsvpsAsync(int bookingId)
    {
        var b = await GroupBookingAsync(bookingId);
        var members = (await uow.GetRepository<GroupMember>().GetByConditionAsync(m => m.GroupId == b.GroupId)).Select(m => m.UserId).ToArray();
        var rows = await uow.GetRepository<GroupRsvp>().GetByConditionAsync(r => r.BookingId == bookingId && members.Contains(r.UserId));
        var users = (await uow.GetRepository<User>().GetByConditionAsync(u => members.Contains(u.Id))).ToDictionary(u => u.Id, u => u.FullName);
        return rows.Select(r => new RsvpView(r.UserId, users.GetValueOrDefault(r.UserId) ?? "", r.Attending)).ToArray();
    }
    public async Task InviteMembersAsync(int bookingId, int[] userIds)
    {
        if (userIds is not { Length: > 0 and <= 50 } || userIds.Distinct().Count() != userIds.Length)
            throw new BadRequestException("Mời từ 1 đến 50 thành viên khác nhau.");
        await uow.RunInTransactionAsync(async () =>
        {
            var b = await GroupBookingAsync(bookingId);
            await uow.LockGroupAsync(b.GroupId!.Value);
            b = await GroupBookingAsync(bookingId);
            if (b.UserId != access.ActorId)
                throw new ConflictException("Chỉ người đặt được mời thành viên cho đơn.");
            await EnsureActiveAsync(b);
            var members = (await uow.GetRepository<GroupMember>().GetByConditionAsync(m => m.GroupId == b.GroupId && userIds.Contains(m.UserId))).Select(m => m.UserId).ToArray();
            if (members.Length != userIds.Length)
                throw new ConflictException("Chỉ mời thành viên hiện tại của nhóm.");
            var repo = uow.GetRepository<GroupRsvp>();
            foreach (var userId in userIds)
            {
                if (await repo.CountByConditionAsync(r => r.BookingId == bookingId && r.UserId == userId) == 0)
                    await repo.AddAsync(new()
                    {
                        BookingId = bookingId,
                        UserId = userId
                    });
                await inbox.PublishAsync(userId, null, $"rsvp:{bookingId}", "Lời mời tham gia buổi chơi", $"Bạn được mời tham gia đơn nhóm {b.BookingCode}. Mở nhóm để xác nhận tham gia.");
            }
            await uow.SaveChangesAsync();
        });
    }
    public Task RespondAsync(int bookingId, bool attending) => uow.RunInTransactionAsync(async () =>
    {
        var b = await GroupBookingAsync(bookingId);
        await uow.LockGroupAsync(b.GroupId!.Value);
        b = await GroupBookingAsync(bookingId);
        await EnsureActiveAsync(b);
        var repo = uow.GetRepository<GroupRsvp>();
        var row = (await repo.GetByConditionAsync(r => r.BookingId == bookingId && r.UserId == access.ActorId)).SingleOrDefault() ?? throw new ConflictException("Bạn chưa được mời vào buổi chơi này.");
        if (attending && row.Attending != true)
        {
            var members = (await uow.GetRepository<GroupMember>().GetByConditionAsync(m => m.GroupId == b.GroupId)).Select(m => m.UserId).ToArray();
            var accepted = await repo.CountByConditionAsync(r => r.BookingId == bookingId && r.Attending == true && r.UserId != access.ActorId && members.Contains(r.UserId));
            if (accepted >= b.PlayerCount)
                throw new ConflictException("Đã đủ số người của đơn; liên hệ người đặt.");
        }
        row.Attending = attending;
        row.UpdatedAt = DateTime.UtcNow;
        repo.Update(row);
        await uow.SaveChangesAsync();
    });
    private async Task<Booking> GroupBookingAsync(int id)
    {
        await access.ActorAsync();
        var b = (await uow.GetRepository<Booking>().GetByConditionAsync(b => b.Id == id)).SingleOrDefault() ?? throw new NotFoundException("Booking", id);
        if (b.DeletedAt.HasValue || b.GroupId is null)
            throw new ConflictException("Đơn không thuộc nhóm.");
        var group = (await uow.GetRepository<PlayGroup>().GetByConditionAsync(g => g.Id == b.GroupId.Value)).SingleOrDefault();
        if (group is null || group.DeletedAt.HasValue || await uow.GetRepository<GroupMember>().CountByConditionAsync(m => m.GroupId == b.GroupId && m.UserId == access.ActorId) == 0)
            throw new ConflictException("Chỉ thành viên hiện tại được xem buổi chơi của nhóm.");
        return b;
    }
    private async Task EnsureActiveAsync(Booking b)
    {
        if (b.Status is not (Badminton.Core.Enums.BookingStatus.Pending or Badminton.Core.Enums.BookingStatus.Confirmed))
            throw new ConflictException("Buổi chơi không còn nhận xác nhận tham gia.");
        if (b.Status == Badminton.Core.Enums.BookingStatus.Pending && (b.PendingExpiresAt is null || b.PendingExpiresAt <= clock.UtcNow))
            throw new ConflictException("Đơn giữ chỗ đã hết hạn.");
        var slot = await uow.GetRepository<TimeSlot>().GetByIdAsync(b.SlotId);
        if (slot is null || HanoiTime.SlotEnd(b.Date, slot.EndTime) <= clock.LocalNow)
            throw new ConflictException("Buổi chơi đã kết thúc.");
    }
}
