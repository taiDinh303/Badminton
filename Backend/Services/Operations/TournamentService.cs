using Badminton.Core.Base;
using Badminton.Core.Time;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class TournamentService(IUnitOfWork uow, IOperationsAccess access, IBusinessClock clock) : ITournamentService
{
    private static TournamentView View(MiniTournament x) => new(x.Id, x.BranchId, x.OrganizerId, x.Name, x.MaxTeams, x.Status);
    public async Task<IReadOnlyList<TournamentView>> ListAsync(int? branchId, int page)
    {
        await access.ActorAsync();
        var (rows, _) = await uow.GetRepository<MiniTournament>().GetPagedAsync(x => !branchId.HasValue || x.BranchId == branchId, x => x.CreatedAt, OrderDirection.Desc, new(page, 30));
        return rows.Select(View).ToArray();
    }
    public async Task<int> CreateAsync(TournamentRequest r)
    {
        await access.StaffBranchAsync(r.BranchId);
        ClubInput.Number(r.MaxTeams, 2, 32, "Số đội tối đa");
        var branch = await uow.GetRepository<Branch>().GetByIdAsync(r.BranchId);
        if (branch is null || branch.DeletedAt.HasValue || branch.Status != Badminton.Core.Enums.BranchStatus.Active)
            throw new BadRequestException("Cơ sở không hoạt động.");
        var row = new MiniTournament { BranchId = r.BranchId, OrganizerId = access.ActorId, Name = ClubInput.Text(r.Name, 120, "Tên giải"), MaxTeams = r.MaxTeams };
        await uow.GetRepository<MiniTournament>().AddAsync(row);
        await uow.SaveChangesAsync();
        return row.Id;
    }
    public async Task<TournamentDetail> DetailAsync(int id)
    {
        await access.ActorAsync();
        var row = await Find(id);
        var matches = await uow.GetRepository<TournamentMatch>().GetByConditionAsync(x => x.TournamentId == id);
        var teams = await uow.GetRepository<TournamentTeam>().GetByConditionAsync(x => x.TournamentId == id);
        var standings = teams.Select(t =>
        {
            var played = matches.Where(x => x.ScoreA.HasValue && (x.TeamAId == t.Id || x.TeamBId == t.Id)).ToArray();
            return new TeamView(t.Id, t.CaptainId, t.Name, t.Players, t.Pool, played.Length, played.Count(x => x.TeamAId == t.Id ? x.ScoreA > x.ScoreB : x.ScoreB > x.ScoreA), played.Sum(x => (x.TeamAId == t.Id ? x.ScoreA : x.ScoreB) ?? 0), played.Sum(x => (x.TeamAId == t.Id ? x.ScoreB : x.ScoreA) ?? 0));
        }).OrderBy(x => x.Pool).ThenByDescending(x => x.Won).ThenByDescending(x => x.PointsFor - x.PointsAgainst).ThenBy(x => x.Id).ToArray();
        return new(View(row), standings, matches.OrderBy(x => x.Pool).ThenBy(x => x.Round).ThenBy(x => x.Id).Select(x => new MatchView(x.Id, x.TeamAId, x.TeamBId, x.Pool, x.Round, x.StartsAtUtc, x.EndsAtUtc, x.Location, x.ScoreA, x.ScoreB)).ToArray());
    }
    public Task RegisterAsync(int id, TeamRequest r) => Change(id, async row =>
    {
        if (row.Status != "Registration")
            throw new ConflictException("Giải đã đóng đăng ký.");
        var name = ClubInput.Text(r.Name, 120, "Tên đội");
        var players = ClubInput.Text(r.Players, 1000, "Thành viên (tên hiển thị)");
        var repo = uow.GetRepository<TournamentTeam>();
        var teams = await repo.GetByConditionAsync(x => x.TournamentId == id);
        var old = teams.SingleOrDefault(x => x.CaptainId == access.ActorId);
        if (old is not null)
        {
            if (old.Name == name && old.Players == players)
                return;
            throw new ConflictException("Mỗi tài khoản đăng ký một đội; rút đội trước khi đổi thông tin.");
        }
        if (teams.Count >= row.MaxTeams)
            throw new ConflictException("Giải đã đủ đội.");
        if (teams.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new ConflictException("Tên đội đã tồn tại.");
        await repo.AddAsync(new()
        {
            TournamentId = id,
            CaptainId = access.ActorId,
            Name = name,
            Players = players
        });
    });
    public Task WithdrawAsync(int id) => Change(id, async row =>
    {
        if (row.Status != "Registration")
            throw new ConflictException("Chỉ rút đội trước khi chia bảng.");
        var repo = uow.GetRepository<TournamentTeam>();
        var team = (await repo.GetByConditionAsync(x => x.TournamentId == id && x.CaptainId == access.ActorId)).SingleOrDefault();
        if (team is not null)
            await repo.RemoveAsync((await repo.GetFreshByIdAsync(team.Id))!);
    });
    public Task DrawAsync(int id, int pools) => Change(id, async row =>
    {
        await access.StaffBranchAsync(row.BranchId);
        if (row.Status != "Registration")
            throw new ConflictException("Giải đã chia bảng; không tạo lịch trùng.");
        var teams = (await uow.GetRepository<TournamentTeam>().GetByConditionAsync(x => x.TournamentId == id)).OrderBy(x => x.Id).ToArray();
        ClubInput.Number(pools, 1, 8, "Số bảng");
        if (teams.Length < 2 * pools)
            throw new ConflictException("Mỗi bảng cần ít nhất hai đội.");
        for (var i = 0; i < teams.Length; i++)
        {
            teams[i] = (await uow.GetRepository<TournamentTeam>().GetFreshByIdAsync(teams[i].Id))!;
            teams[i].Pool = i % pools + 1;
            uow.GetRepository<TournamentTeam>().Update(teams[i]);
        }
        foreach (var pool in teams.GroupBy(x => x.Pool))
        {
            var rotation = pool.Select(x => x.Id).ToList();
            if (rotation.Count % 2 != 0)
                rotation.Add(0);
            for (var round = 1; round < rotation.Count; round++)
            {
                for (var j = 0; j < rotation.Count / 2; j++)
                {
                    var a = rotation[j];
                    var b = rotation[rotation.Count - 1 - j];
                    if (a == 0 || b == 0)
                        continue;
                    await uow.GetRepository<TournamentMatch>().AddAsync(new()
                    {
                        TournamentId = id,
                        TeamAId = Math.Min(a, b),
                        TeamBId = Math.Max(a, b),
                        Pool = pool.Key,
                        Round = round
                    });
                }
                var last = rotation[^1];
                rotation.RemoveAt(rotation.Count - 1);
                rotation.Insert(1, last);
            }
        }
        row.Status = "Scheduled";
        row.UpdatedAt = clock.UtcNow;
        uow.GetRepository<MiniTournament>().Update(row);
    });
    public Task ScheduleAsync(int id, int matchId, MatchScheduleRequest r) => Change(id, async row =>
    {
        await access.StaffBranchAsync(row.BranchId);
        EnsureRunning(row);
        ClubInput.Range(r.StartsAtUtc, r.EndsAtUtc, TimeSpan.FromHours(4));
        if (r.StartsAtUtc <= clock.UtcNow || r.StartsAtUtc > clock.UtcNow.AddDays(180))
            throw new BadRequestException("Chọn lịch đấu trong 180 ngày tới.");
        var location = ClubInput.Text(r.Location, 120, "Sân thi đấu");
        await uow.LockNotificationAsync($"tournament-venue:{row.BranchId}");
        var match = await Match(id, matchId);
        if (match.ScoreA.HasValue)
            throw new ConflictException("Trận đã có kết quả.");
        var activeIds = (await uow.GetRepository<MiniTournament>().GetByConditionAsync(x => x.BranchId == row.BranchId && x.Status != "Cancelled")).Select(x => x.Id).ToArray();
        var others = await uow.GetRepository<TournamentMatch>().GetByConditionAsync(x => activeIds.Contains(x.TournamentId) && x.Id != matchId && x.StartsAtUtc < r.EndsAtUtc && x.EndsAtUtc > r.StartsAtUtc);
        if (others.Any(x => string.Equals(x.Location, location, StringComparison.OrdinalIgnoreCase) || x.TeamAId == match.TeamAId || x.TeamBId == match.TeamAId || x.TeamAId == match.TeamBId || x.TeamBId == match.TeamBId))
            throw new ConflictException("Trùng giờ của đội hoặc sân thi đấu.");
        match.StartsAtUtc = r.StartsAtUtc;
        match.EndsAtUtc = r.EndsAtUtc;
        match.Location = location;
        match.UpdatedAt = clock.UtcNow;
        uow.GetRepository<TournamentMatch>().Update(match);
    });
    public Task ScoreAsync(int id, int matchId, MatchScoreRequest r) => Change(id, async row =>
    {
        await access.StaffBranchAsync(row.BranchId);
        if (row.Status is not ("Scheduled" or "Completed"))
            throw new ConflictException("Giải chưa chia bảng hoặc đã hủy.");
        ClubInput.Number(r.ScoreA, 0, 99, "Điểm A");
        ClubInput.Number(r.ScoreB, 0, 99, "Điểm B");
        if (r.ScoreA == r.ScoreB)
            throw new BadRequestException("Trận cầu lông phải có đội thắng.");
        var match = await Match(id, matchId);
        if (match.EndsAtUtc is null || match.EndsAtUtc > clock.UtcNow)
            throw new ConflictException("Chỉ ghi kết quả sau giờ kết thúc lịch đấu.");
        var repo = uow.GetRepository<TournamentScoreRevision>();
        var history = await repo.GetByConditionAsync(x => x.MatchId == matchId);
        var revision = history.Count == 0 ? 0 : history.Max(x => x.Revision);
        if (match.ScoreA == r.ScoreA && match.ScoreB == r.ScoreB)
            return;
        if ((match.ScoreA.HasValue && r.ExpectedRevision != revision) || (r.ExpectedRevision.HasValue && r.ExpectedRevision != revision))
            throw new ConflictException("Kết quả đã thay đổi. Tải lại lịch sử trước khi sửa.");
        var reason = match.ScoreA.HasValue ? ClubInput.Text(r.Reason, 1000, "Lý do sửa kết quả") : string.IsNullOrWhiteSpace(r.Reason) ? "Ghi nhận kết quả lần đầu" : ClubInput.Text(r.Reason, 1000, "Lý do");
        await repo.AddAsync(new()
        {
            MatchId = matchId,
            ActorId = access.ActorId,
            Revision = revision + 1,
            OldScoreA = match.ScoreA,
            OldScoreB = match.ScoreB,
            ScoreA = r.ScoreA,
            ScoreB = r.ScoreB,
            Reason = reason,
            CreatedAt = clock.UtcNow
        });
        match.ScoreA = r.ScoreA;
        match.ScoreB = r.ScoreB;
        match.UpdatedAt = clock.UtcNow;
        uow.GetRepository<TournamentMatch>().Update(match);
        var pending = await uow.GetRepository<TournamentMatch>().CountByConditionAsync(x => x.TournamentId == id && x.Id != matchId && x.ScoreA == null);
        row.Status = pending == 0 ? "Completed" : "Scheduled";
        row.UpdatedAt = clock.UtcNow;
        uow.GetRepository<MiniTournament>().Update(row);
    });
    public async Task<IReadOnlyList<ScoreRevisionView>> ScoreHistoryAsync(int id, int matchId)
    {
        await access.ActorAsync();
        await Match(id, matchId);
        return (await uow.GetRepository<TournamentScoreRevision>().GetByConditionAsync(x => x.MatchId == matchId)).OrderBy(x => x.Revision).Select(x => new ScoreRevisionView(x.Revision, x.ActorId, x.OldScoreA, x.OldScoreB, x.ScoreA, x.ScoreB, x.Reason, x.CreatedAt)).ToArray();
    }
    public Task CancelAsync(int id) => Change(id, async row =>
    {
        await access.StaffBranchAsync(row.BranchId);
        if (row.Status == "Completed")
            throw new ConflictException("Giải đã hoàn thành.");
        row.Status = "Cancelled";
        row.UpdatedAt = clock.UtcNow;
        uow.GetRepository<MiniTournament>().Update(row);
    });
    private Task Change(int id, Func<MiniTournament, Task> action) => uow.RunInTransactionAsync(async () =>
    {
        await access.ActorAsync();
        await uow.LockNotificationAsync($"tournament:{id}");
        await action(await Find(id));
        await uow.SaveChangesAsync();
    });
    private static void EnsureRunning(MiniTournament row)
    {
        if (row.Status != "Scheduled")
            throw new ConflictException("Giải chưa chia bảng hoặc đã đóng.");
    }
    private async Task<MiniTournament> Find(int id) => await uow.GetRepository<MiniTournament>().GetFreshByIdAsync(id) ?? throw new NotFoundException("Giải đấu", id);
    private async Task<TournamentMatch> Match(int id, int matchId)
    {
        var row = await uow.GetRepository<TournamentMatch>().GetFreshByIdAsync(matchId);
        return row is not null && row.TournamentId == id ? row : throw new NotFoundException("Trận đấu", matchId);
    }
}
