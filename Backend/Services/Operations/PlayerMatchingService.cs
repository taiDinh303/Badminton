using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Time;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class PlayerMatchingService(IUnitOfWork uow, IOperationsAccess access, IBusinessClock clock) : IPlayerMatchingService
{
    public async Task<IReadOnlyList<PlayerSessionView>> SearchAsync(int? branchId, string? area, int? level, DateTime fromUtc, DateTime toUtc, int page)
    {
        await access.ActorAsync();
        ClubInput.Range(fromUtc, toUtc, TimeSpan.FromDays(31));
        if (level.HasValue)
            ClubInput.Number(level.Value, 1, 5, "Trình độ");
        var region = (area ?? "").Trim();
        if (region.Length > 100)
            throw new BadRequestException("Khu vực quá dài.");
        var (rows, _) = await uow.GetRepository<PlayerSession>().GetPagedAsync(x => !x.Closed && x.StartsAtUtc >= fromUtc && x.EndsAtUtc <= toUtc && x.StartsAtUtc > clock.UtcNow && (!branchId.HasValue || x.BranchId == branchId) && (!level.HasValue || x.Level == level) && (region == "" || x.Area.Contains(region)), x => x.StartsAtUtc, OrderDirection.Asc, new(page, 30));
        var ids = rows.Select(x => x.Id).ToArray();
        var owners = rows.Select(x => x.OwnerId).ToArray();
        var names = (await uow.GetRepository<User>().GetByConditionAsync(x => owners.Contains(x.Id))).ToDictionary(x => x.Id, x => x.FullName);
        var members = await uow.GetRepository<PlayerSessionMember>().GetByConditionAsync(x => ids.Contains(x.SessionId));
        return rows.Select(x => new PlayerSessionView(x.Id, x.OwnerId, names.GetValueOrDefault(x.OwnerId) ?? "", x.BranchId, x.Area, x.Level, x.StartsAtUtc, x.EndsAtUtc, x.Capacity, 1 + members.Count(m => m.SessionId == x.Id && m.Status == "Accepted"), x.Note, x.Closed, members.SingleOrDefault(m => m.SessionId == x.Id && m.UserId == access.ActorId)?.Status)).ToArray();
    }
    public async Task<int> CreateAsync(PlayerSessionRequest r)
    {
        await access.ActorAsync();
        ClubInput.Range(r.StartsAtUtc, r.EndsAtUtc, TimeSpan.FromHours(8));
        if (r.StartsAtUtc <= clock.UtcNow || r.StartsAtUtc > clock.UtcNow.AddDays(90))
            throw new BadRequestException("Chọn buổi chơi trong 90 ngày tới.");
        ClubInput.Number(r.Level, 1, 5, "Trình độ");
        ClubInput.Number(r.Capacity, 2, 30, "Số người gồm chủ buổi");
        var branch = await uow.GetRepository<Branch>().GetByIdAsync(r.BranchId);
        if (branch is null || branch.DeletedAt.HasValue || branch.Status != BranchStatus.Active)
            throw new BadRequestException("Cơ sở không hoạt động.");
        var row = new PlayerSession { OwnerId = access.ActorId, BranchId = r.BranchId, Area = ClubInput.Text(r.Area, 100, "Khu vực"), Level = r.Level, StartsAtUtc = r.StartsAtUtc, EndsAtUtc = r.EndsAtUtc, Capacity = r.Capacity, Note = ClubInput.Text(r.Note, 1000, "Nội dung") };
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockNotificationAsync($"player-owner:{access.ActorId}");
            if (await uow.GetRepository<PlayerSession>().CountByConditionAsync(x => x.OwnerId == access.ActorId && !x.Closed && x.EndsAtUtc > clock.UtcNow) >= 10)
                throw new ConflictException("Tối đa 10 buổi đang mở.");
            await uow.GetRepository<PlayerSession>().AddAsync(row);
            await uow.SaveChangesAsync();
        });
        return row.Id;
    }
    public Task RequestAsync(int id) => Change(id, async row =>
    {
        if (row.OwnerId == access.ActorId)
            throw new ConflictException("Bạn là chủ buổi chơi.");
        var repo = uow.GetRepository<PlayerSessionMember>();
        var member = (await repo.GetByConditionAsync(x => x.SessionId == id && x.UserId == access.ActorId)).SingleOrDefault();
        if (member is not null) member = await repo.GetFreshByIdAsync(member.Id);
        if (member?.Status is "Requested" or "Accepted")
            return;
        if (1 + await repo.CountByConditionAsync(x => x.SessionId == id && x.Status == "Accepted") >= row.Capacity)
            throw new ConflictException("Buổi chơi đã đủ người.");
        if (member is null)
            await repo.AddAsync(new()
            {
                SessionId = id,
                UserId = access.ActorId
            });
        else
        {
            member.Status = "Requested";
            repo.Update(member);
        }
    });
    public async Task<IReadOnlyList<SessionMemberView>> MembersAsync(int id)
    {
        await access.ActorAsync();
        var row = await Find(id);
        if (row.OwnerId != access.ActorId)
            throw new ConflictException("Chỉ chủ buổi được xem yêu cầu tham gia.");
        var members = await uow.GetRepository<PlayerSessionMember>().GetByConditionAsync(x => x.SessionId == id && (x.Status == "Requested" || x.Status == "Accepted"));
        var ids = members.Select(x => x.UserId).ToArray();
        var names = (await uow.GetRepository<User>().GetByConditionAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id, x => x.FullName);
        return members.Select(x => new SessionMemberView(x.UserId, names.GetValueOrDefault(x.UserId) ?? "", x.Status)).ToArray();
    }
    public Task DecideAsync(int id, int userId, bool accept) => Change(id, async row =>
    {
        if (row.OwnerId != access.ActorId)
            throw new ConflictException("Chỉ chủ buổi được duyệt.");
        var repo = uow.GetRepository<PlayerSessionMember>();
        var member = (await repo.GetByConditionAsync(x => x.SessionId == id && x.UserId == userId)).SingleOrDefault() ?? throw new NotFoundException("Yêu cầu", userId);
        member = await repo.GetFreshByIdAsync(member.Id) ?? throw new NotFoundException("Yêu cầu", userId);
        if (member.Status == (accept ? "Accepted" : "Rejected"))
            return;
        if (member.Status != "Requested")
            throw new ConflictException("Yêu cầu không còn chờ duyệt.");
        if (accept)
        {
            var user = await uow.GetRepository<User>().GetByIdAsync(userId) ?? throw new NotFoundException("User", userId);
            if (user.Status != UserStatus.Active || user.DeletedAt.HasValue || !user.IsVerified)
                throw new ConflictException("Người tham gia không còn hoạt động.");
            if (1 + await repo.CountByConditionAsync(x => x.SessionId == id && x.Status == "Accepted") >= row.Capacity)
                throw new ConflictException("Đã đủ người.");
        }
        member.Status = accept ? "Accepted" : "Rejected";
        member.UpdatedAt = clock.UtcNow;
        repo.Update(member);
    });
    public Task LeaveAsync(int id) => Change(id, async _ =>
    {
        var repo = uow.GetRepository<PlayerSessionMember>();
        var member = (await repo.GetByConditionAsync(x => x.SessionId == id && x.UserId == access.ActorId)).SingleOrDefault();
        if (member is not null) member = await repo.GetFreshByIdAsync(member.Id);
        if (member is not null)
        {
            member.Status = "Left";
            repo.Update(member);
        }
    });
    public Task CloseAsync(int id) => Change(id, row =>
    {
        if (row.OwnerId != access.ActorId)
            throw new ConflictException("Chỉ chủ buổi được đóng.");
        row.Closed = true;
        row.UpdatedAt = clock.UtcNow;
        uow.GetRepository<PlayerSession>().Update(row);
        return Task.CompletedTask;
    }, true);
    private async Task<PlayerSession> Find(int id) => await uow.GetRepository<PlayerSession>().GetFreshByIdAsync(id) ?? throw new NotFoundException("Buổi chơi", id);
    private Task Change(int id, Func<PlayerSession, Task> action, bool allowClosed = false) => uow.RunInTransactionAsync(async () =>
    {
        await access.ActorAsync();
        await uow.LockNotificationAsync($"player-session:{id}");
        var row = await Find(id);
        if (!allowClosed && (row.Closed || row.StartsAtUtc <= clock.UtcNow))
            throw new ConflictException("Buổi chơi đã đóng hoặc bắt đầu.");
        await action(row);
        await uow.SaveChangesAsync();
    });
}
