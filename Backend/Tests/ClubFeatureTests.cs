using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Badminton.Core.Time;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Contract.Repositories.Entity;
using Contract.Services.Booking;
using Contract.Services.Group;
using Contract.Services.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelViews.Operations;
using Repositories.Context;
using Xunit;
namespace Badminton.Tests;

public sealed class ClubFeatureTests
{
    [Fact]
    public async Task Reused_scope_refreshes_inventory_and_can_complete_workflows_without_stale_entities()
    {
        await using var h = Host(); h.ActAs(h.Manager, Role.Manager);
        await h.RunScopeAsync(async sp => {
            var equipment = sp.GetRequiredService<IEquipmentService>();
            var item = await equipment.SaveItemAsync(null, new(h.BranchId, "Cached racket", true, 1, 20000, true));
            await h.RunScopeAsync(other => other.GetRequiredService<BadmintonBookingDbContext>().Set<EquipmentItem>().Where(x => x.Id == item).ExecuteUpdateAsync(s => s.SetProperty(x => x.Stock, 0)));
            var request = new EquipmentOrderRequest(item, 1, h.HanoiNowUtc.AddHours(3), "cached-order");
            await Assert.ThrowsAsync<ConflictException>(() => equipment.ReserveAsync(request));
            await h.RunScopeAsync(other => other.GetRequiredService<BadmintonBookingDbContext>().Set<EquipmentItem>().Where(x => x.Id == item).ExecuteUpdateAsync(s => s.SetProperty(x => x.Stock, 1)));
            var order = await equipment.ReserveAsync(request);
            await equipment.TransitionAsync(order, "issue"); await equipment.TransitionAsync(order, "return");
            Assert.Equal(1, Assert.Single(await equipment.ItemsAsync(h.BranchId)).Available);
            var coaching = sp.GetRequiredService<ICoachingService>();
            var coach = await coaching.SaveCoachAsync(null, new(h.BranchId, "Cached coach", "Synthetic", true));
            var slot = await coaching.AddSlotAsync(coach, new(h.HanoiNowUtc.AddHours(1), h.HanoiNowUtc.AddHours(2), 1, 100000));
            await ClubCourtFixture.AttachAsync(h, slot);
            await coaching.ReserveAsync(new(slot, 1, "cached-class"));
            await coaching.CancelSlotAsync(slot);
            Assert.Equal("Cancelled", Assert.Single(await coaching.ReservationsAsync(false, null, 1)).Status);
            var calendar = sp.GetRequiredService<ICalendarSubscriptionService>();
            var first = await calendar.RotateAsync(); var second = await calendar.RotateAsync();
            await Assert.ThrowsAsync<NotFoundException>(() => calendar.FeedAsync(first.Token));
            await calendar.RevokeAsync(); await Assert.ThrowsAsync<NotFoundException>(() => calendar.FeedAsync(second.Token));
            var tournaments = sp.GetRequiredService<ITournamentService>();
            var tournament = await tournaments.CreateAsync(new(h.BranchId, "Cached tournament", 2));
            await tournaments.RegisterAsync(tournament, new("Manager team", "Manager"));
            h.ActAs(h.Alice, Role.Customer); await tournaments.RegisterAsync(tournament, new("Alice team", "Alice"));
            h.ActAs(h.Manager, Role.Manager); await tournaments.DrawAsync(tournament, 1);
            Assert.Single((await tournaments.DetailAsync(tournament)).Matches);
            HanoiTime.NowUtc = () => h.HanoiNowUtc.AddDays(1);
            Assert.Equal(order, await equipment.ReserveAsync(request));
        });
    }
    [Fact]
    public async Task Club_migration_upgrades_preserves_existing_rows_and_rejects_populated_downgrade()
    {
        await using var h = Host(true);
        await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var migrations = db.Database.GetMigrations().ToArray();
            var newest = Array.FindIndex(migrations, x => x.EndsWith("_AddClubFeatures"));
            Assert.True(newest > 0);
            await db.GetService<IMigrator>().MigrateAsync(migrations[newest - 1]);
            Assert.Equal("Alice", (await db.Users.SingleAsync(x => x.Id == h.Alice)).FullName);
            await db.GetService<IMigrator>().MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(49, db.Model.GetEntityTypes().Select(x => x.GetTableName()).Distinct().Count());
            if (db.Database.IsNpgsql())
            {
                var enabled = await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_class WHERE relrowsecurity AND relname IN ('CalendarSubscriptions','CoachProfiles','CoachSlots','CoachingReservations','EquipmentItems','EquipmentOrders','GroupExpenses','GroupExpenseShares','MiniTournaments','PlayerSessions','PlayerSessionMembers','TournamentTeams','TournamentMatches')").SingleAsync();
                Assert.Equal(13, enabled);
            }
        });
        h.ActAs(h.Manager, Role.Manager);
        await Call<IEquipmentService, int>(h, s => s.SaveItemAsync(null, new(h.BranchId, "Migration sentinel", true, 1, 20000, true)));
        await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var migrations = db.Database.GetMigrations().ToArray();
            var newest = Array.FindIndex(migrations, x => x.EndsWith("_AddClubFeatures"));
            await Assert.ThrowsAnyAsync<Exception>(() => db.GetService<IMigrator>().MigrateAsync(migrations[newest - 1]));
            Assert.Equal("Migration sentinel", (await db.Set<EquipmentItem>().SingleAsync()).Name);
        });
    }
    private sealed class ClubClock : IBusinessClock
    {
        public DateTime UtcNow => HanoiTime.NowUtc();
        public DateTime LocalNow => HanoiTime.HanoiNow();
        public DateOnly Today => HanoiTime.HanoiToday();
    }
    private static TestHost Host(bool migrations = false) => new(configure: s => s.AddSingleton<IBusinessClock, ClubClock>(), useMigrations: migrations);
    private static Task<T> Call<S, T>(TestHost h, Func<S, Task<T>> f) where S : notnull => h.RunScopeAsync(sp => f(sp.GetRequiredService<S>()));
    private static Task Call<S>(TestHost h, Func<S, Task> f) where S : notnull => h.RunScopeAsync(sp => f(sp.GetRequiredService<S>()));
    private static async Task<bool> Try(Func<Task> f)
    {
        try
        {
            await f();
            return true;
        }
        catch (ConflictException) { return false; }
    }

    [Fact]
    public async Task Matching_filters_by_skill_area_branch_and_full_availability_window()
    {
        await using var h = Host();
        h.ActAs(h.Alice, Role.Customer);
        var r = new PlayerSessionRequest(h.BranchId, "Thủ Đức", 3, h.HanoiNowUtc.AddHours(2), h.HanoiNowUtc.AddHours(4), 4, "Tìm bạn đánh đôi");
        var id = await Call<IPlayerMatchingService, int>(h, s => s.CreateAsync(r));
        async Task<IReadOnlyList<PlayerSessionView>> Search(int? branch, string area, int? level, DateTime from, DateTime to) => await Call<IPlayerMatchingService, IReadOnlyList<PlayerSessionView>>(h, s => s.SearchAsync(branch, area, level, from, to, 1));
        Assert.Equal(id, Assert.Single(await Search(h.BranchId, "Thủ", 3, h.HanoiNowUtc, h.HanoiNowUtc.AddDays(1))).Id);
        Assert.Empty(await Search(h.BranchId2, "", null, h.HanoiNowUtc, h.HanoiNowUtc.AddDays(1)));
        Assert.Empty(await Search(h.BranchId, "Bình Thạnh", 3, h.HanoiNowUtc, h.HanoiNowUtc.AddDays(1)));
        Assert.Empty(await Search(h.BranchId, "", 2, h.HanoiNowUtc, h.HanoiNowUtc.AddDays(1)));
        Assert.Empty(await Search(h.BranchId, "", null, h.HanoiNowUtc.AddHours(3), h.HanoiNowUtc.AddHours(5)));
        await Assert.ThrowsAsync<BadRequestException>(() => Call<IPlayerMatchingService, int>(h, s => s.CreateAsync(r with { Level = 6 })));
    }
    [Fact]
    public async Task Matching_approval_serializes_last_place_and_preserves_private_member_list()
    {
        await using var h = Host();
        h.ActAs(h.Alice, Role.Customer);
        var id = await Call<IPlayerMatchingService, int>(h, s => s.CreateAsync(new(h.BranchId, "Quận 1", 2, h.HanoiNowUtc.AddHours(2), h.HanoiNowUtc.AddHours(3), 2, "Đánh đơn")));
        foreach (var user in new[] { h.Bob, h.Carol })
        {
            h.ActAs(user, Role.Customer);
            await Call<IPlayerMatchingService>(h, s => s.RequestAsync(id));
        }
        await Assert.ThrowsAsync<ConflictException>(() => Call<IPlayerMatchingService, IReadOnlyList<SessionMemberView>>(h, s => s.MembersAsync(id)));
        h.ActAs(h.Alice, Role.Customer);
        var accepted = await Task.WhenAll(new[] { h.Bob, h.Carol }.Select(user => Try(() => Call<IPlayerMatchingService>(h, s => s.DecideAsync(id, user, true)))));
        Assert.Single(accepted, x => x);
        var members = await Call<IPlayerMatchingService, IReadOnlyList<SessionMemberView>>(h, s => s.MembersAsync(id));
        Assert.Single(members, x => x.Status == "Accepted");
        var winner = members.Single(x => x.Status == "Accepted").UserId;
        h.ActAs(winner, Role.Customer);
        await Call<IPlayerMatchingService>(h, s => s.LeaveAsync(id));
        h.ActAs(h.Alice, Role.Customer);
        await Call<IPlayerMatchingService>(h, s => s.CloseAsync(id));
        h.ActAs(h.Bob, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IPlayerMatchingService>(h, s => s.RequestAsync(id)));
    }
    [Fact]
    public async Task Expense_split_preserves_exact_total_owner_controls_and_paid_history_after_cancellation()
    {
        await using var h = Host();
        h.ActAs(h.Alice, Role.Customer);
        var group = await Call<IGroupService, ModelViews.Group.GroupView>(h, s => s.CreateAsync("Chi phí", h.Alice));
        h.ActAs(h.Bob, Role.Customer);
        await Call<IGroupService>(h, s => s.JoinAsync(group.Id, h.Bob, group.InviteCode));
        h.ActAs(h.Alice, Role.Customer);
        var b = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CreateAsync(new(h.Court1, h.FutureDate.ToString("yyyy-MM-dd"), h.SlotNoon, 2, group.Id), h.Alice));
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(x => x.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BookingStatus.Confirmed).SetProperty(x => x.PayableAmount, 100001)));
        await Call<IGroupExpenseService>(h, s => s.CreateAsync(b.Id, [h.Alice, h.Bob]));
        var expense = await Call<IGroupExpenseService, ExpenseView?>(h, s => s.GetAsync(b.Id));
        Assert.Equal(100001, expense!.Shares.Sum(x => x.Amount));
        Assert.Equal(1, expense.Shares.Max(x => x.Amount) - expense.Shares.Min(x => x.Amount));
        h.ActAs(h.Bob, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IGroupExpenseService>(h, s => s.RecordAsync(b.Id, h.Bob, true)));
        h.ActAs(h.Carol, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IGroupExpenseService, ExpenseView?>(h, s => s.GetAsync(b.Id)));
        h.ActAs(h.Alice, Role.Customer);
        await Call<IGroupExpenseService>(h, s => s.RecordAsync(b.Id, h.Bob, true));
        await Call<IGroupExpenseService>(h, s => s.RecordAsync(b.Id, h.Bob, true));
        await Assert.ThrowsAsync<ConflictException>(() => Call<IGroupExpenseService>(h, s => s.CreateAsync(b.Id, [h.Alice])));
        Assert.Single((await Call<IGroupExpenseService, ExpenseView?>(h, s => s.GetAsync(b.Id)))!.Shares, x => x.PaidAt.HasValue);
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(x => x.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BookingStatus.Cancelled)));
        Assert.True((await Call<IGroupExpenseService, ExpenseView?>(h, s => s.GetAsync(b.Id)))!.BookingCancelled);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IGroupExpenseService>(h, s => s.RecordAsync(b.Id, h.Alice, true)));
        await Call<IGroupExpenseService>(h, s => s.RecordAsync(b.Id, h.Bob, false));
        Assert.Equal(2, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Set<BookingHistory>().CountAsync(x => x.BookingId == b.Id && x.Event == "group-contribution")));
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Equipment_concurrent_last_unit_replay_delivery_return_and_branch_guards(bool rental)
    {
        await using var h = Host();
        h.ActAs(h.Manager, Role.Manager);
        var itemRequest = new EquipmentItemRequest(h.BranchId, rental ? "Vợt" : "Cầu", rental, 1, 20000, true);
        var item = await Call<IEquipmentService, int>(h, s => s.SaveItemAsync(null, itemRequest));
        h.ActAs(h.Alice, Role.Customer);
        var due = h.HanoiNowUtc.AddHours(5);
        var request = new EquipmentOrderRequest(item, 1, due, "hold-1");
        var ids = new System.Collections.Concurrent.ConcurrentBag<int>();
        var result = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Try(async () => ids.Add(await Call<IEquipmentService, int>(h, s => s.ReserveAsync(request with { RequestKey = $"hold-{i}" }))))));
        Assert.Single(result, x => x);
        var id = Assert.Single(ids);
        var orders = await Call<IEquipmentService, IReadOnlyList<EquipmentOrderView>>(h, s => s.OrdersAsync(false, null, 1));
        Assert.Single(orders);
        Assert.Equal(20000, orders[0].Total);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IEquipmentService>(h, s => s.TransitionAsync(id, "issue")));
        h.ActAs(h.ManagerOtherBranch, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IEquipmentService>(h, s => s.TransitionAsync(id, "issue")));
        h.ActAs(h.Manager, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IEquipmentService, int>(h, s => s.SaveItemAsync(item, itemRequest with { Stock = 0 })));
        await Call<IEquipmentService>(h, s => s.TransitionAsync(id, "issue"));
        await Call<IEquipmentService>(h, s => s.TransitionAsync(id, "issue"));
        var inventory = Assert.Single(await Call<IEquipmentService, IReadOnlyList<EquipmentItemView>>(h, s => s.ItemsAsync(h.BranchId)));
        Assert.Equal(0, inventory.Available);
        if (rental)
        {
            await Call<IEquipmentService>(h, s => s.TransitionAsync(id, "return"));
            await Call<IEquipmentService>(h, s => s.TransitionAsync(id, "return"));
            Assert.Equal(1, Assert.Single(await Call<IEquipmentService, IReadOnlyList<EquipmentItemView>>(h, s => s.ItemsAsync(h.BranchId))).Available);
        }
        else
        {
            Assert.Equal(0, inventory.Stock);
            await Assert.ThrowsAsync<ConflictException>(() => Call<IEquipmentService>(h, s => s.TransitionAsync(id, "return")));
        }
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.CountAsync()));
    }
    [Fact]
    public async Task Equipment_idempotency_is_payload_bound_cancel_releases_and_inactive_items_reject()
    {
        await using var h = Host();
        h.ActAs(h.Manager, Role.Manager);
        var r = new EquipmentItemRequest(h.BranchId, "Vợt 2", true, 2, 10000, true);
        var item = await Call<IEquipmentService, int>(h, s => s.SaveItemAsync(null, r));
        h.ActAs(h.Alice, Role.Customer);
        var order = new EquipmentOrderRequest(item, 1, h.HanoiNowUtc.AddHours(2), "same-key");
        var id = await Call<IEquipmentService, int>(h, s => s.ReserveAsync(order));
        Assert.Equal(id, await Call<IEquipmentService, int>(h, s => s.ReserveAsync(order)));
        await Assert.ThrowsAsync<ConflictException>(() => Call<IEquipmentService, int>(h, s => s.ReserveAsync(order with { Quantity = 2 })));
        h.ActAs(h.Bob, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IEquipmentService>(h, s => s.TransitionAsync(id, "cancel")));
        h.ActAs(h.Alice, Role.Customer);
        await Call<IEquipmentService>(h, s => s.TransitionAsync(id, "cancel"));
        Assert.Equal(2, Assert.Single(await Call<IEquipmentService, IReadOnlyList<EquipmentItemView>>(h, s => s.ItemsAsync(h.BranchId))).Available);
        h.ActAs(h.Manager, Role.Manager);
        await Call<IEquipmentService, int>(h, s => s.SaveItemAsync(item, r with { Active = false }));
        h.ActAs(h.Alice, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IEquipmentService, int>(h, s => s.ReserveAsync(order with { RequestKey = "new-key" })));
    }
    [Fact]
    public async Task Coaching_overlap_capacity_replay_and_cancellation_release_seats()
    {
        await using var h = Host();
        h.ActAs(h.Manager, Role.Manager);
        var coach = await Call<ICoachingService, int>(h, s => s.SaveCoachAsync(null, new(h.BranchId, "HLV Lan", "Kỹ thuật cơ bản", true)));
        var r = new CoachSlotRequest(h.HanoiNowUtc.AddHours(2), h.HanoiNowUtc.AddHours(3), 3, 100000);
        var slot = await Call<ICoachingService, int>(h, s => s.AddSlotAsync(coach, r));
        await ClubCourtFixture.AttachAsync(h, slot);
        await Assert.ThrowsAsync<ConflictException>(() => Call<ICoachingService, int>(h, s => s.AddSlotAsync(coach, r with { StartsAtUtc = r.StartsAtUtc.AddMinutes(30) })));
        await Call<ICoachingService, int>(h, s => s.AddSlotAsync(coach, r with { StartsAtUtc = r.EndsAtUtc, EndsAtUtc = r.EndsAtUtc.AddHours(1) }));
        h.ActAs(h.Alice, Role.Customer);
        var reservation = new CoachingRequest(slot, 2, "learn-a");
        var id = await Call<ICoachingService, int>(h, s => s.ReserveAsync(reservation));
        Assert.Equal(id, await Call<ICoachingService, int>(h, s => s.ReserveAsync(reservation)));
        await Assert.ThrowsAsync<ConflictException>(() => Call<ICoachingService, int>(h, s => s.ReserveAsync(reservation with { People = 1 })));
        h.ActAs(h.Bob, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => Call<ICoachingService, int>(h, s => s.ReserveAsync(reservation with { RequestKey = "learn-b" })));
        await Call<ICoachingService, int>(h, s => s.ReserveAsync(new(slot, 1, "learn-c")));
        h.ActAs(h.ManagerOtherBranch, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => Call<ICoachingService>(h, s => s.CancelSlotAsync(slot)));
        h.ActAs(h.Alice, Role.Customer);
        await Call<ICoachingService>(h, s => s.TransitionAsync(id, "cancel"));
        var slots = await Call<ICoachingService, IReadOnlyList<CoachSlotView>>(h, s => s.SlotsAsync(coach, h.HanoiNowUtc, h.HanoiNowUtc.AddDays(1)));
        Assert.Equal(2, slots.Single(x => x.Id == slot).Available);
        h.ActAs(h.Manager, Role.Manager);
        await Call<ICoachingService>(h, s => s.CancelSlotAsync(slot));
        h.ActAs(h.Bob, Role.Customer);
        Assert.Equal("Cancelled", Assert.Single(await Call<ICoachingService, IReadOnlyList<CoachingView>>(h, s => s.ReservationsAsync(false, null, 1))).Status);
        await Assert.ThrowsAsync<ConflictException>(() => Call<ICoachingService, int>(h, s => s.ReserveAsync(new(slot, 1, "closed"))));
    }
    [Fact]
    public async Task Coaching_last_place_is_atomic_and_completion_obeys_end_time()
    {
        await using var h = Host();
        h.ActAs(h.Manager, Role.Manager);
        var coach = await Call<ICoachingService, int>(h, s => s.SaveCoachAsync(null, new(h.BranchId, "HLV Minh", "Cá nhân", true)));
        var slot = await Call<ICoachingService, int>(h, s => s.AddSlotAsync(coach, new(h.HanoiNowUtc.AddHours(1), h.HanoiNowUtc.AddHours(2), 1, 50000)));
        await ClubCourtFixture.AttachAsync(h, slot);
        h.ActAs(h.Alice, Role.Customer);
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Try(async () => { await Call<ICoachingService, int>(h, s => s.ReserveAsync(new(slot, 1, $"last-{i}"))); })));
        Assert.Single(results, x => x);
        var booking = Assert.Single(await Call<ICoachingService, IReadOnlyList<CoachingView>>(h, s => s.ReservationsAsync(false, null, 1)));
        h.ActAs(h.Manager, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => Call<ICoachingService>(h, s => s.TransitionAsync(booking.Id, "complete")));
        HanoiTime.NowUtc = () => h.HanoiNowUtc.AddHours(2);
        await Call<ICoachingService>(h, s => s.TransitionAsync(booking.Id, "complete"));
        Assert.Equal("Completed", Assert.Single(await Call<ICoachingService, IReadOnlyList<CoachingView>>(h, s => s.ReservationsAsync(true, h.BranchId, 1))).Status);
    }
    [Theory]
    [InlineData(3, 1, 3)]
    [InlineData(4, 1, 6)]
    [InlineData(4, 2, 2)]
    public async Task Tournament_draw_creates_every_pair_once_and_no_team_twice_per_round(int count, int pools, int expected)
    {
        await using var h = Host();
        h.ActAs(h.Manager, Role.Manager);
        var id = await Call<ITournamentService, int>(h, s => s.CreateAsync(new(h.BranchId, "Giải cuối tuần", 8)));
        var users = new[] { h.Alice, h.Bob, h.Carol, h.Manager };
        for (var i = 0; i < count; i++)
        {
            h.ActAs(users[i], i == 3 ? Role.Manager : Role.Customer);
            await Call<ITournamentService>(h, s => s.RegisterAsync(id, new($"Đội {i}", $"Tên {i}")));
        }
        h.ActAs(h.ManagerOtherBranch, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => Call<ITournamentService>(h, s => s.DrawAsync(id, pools)));
        h.ActAs(h.Manager, Role.Manager);
        await Call<ITournamentService>(h, s => s.DrawAsync(id, pools));
        var detail = await Call<ITournamentService, TournamentDetail>(h, s => s.DetailAsync(id));
        Assert.Equal(expected, detail.Matches.Count);
        Assert.Equal(expected, detail.Matches.Select(x => (x.TeamAId, x.TeamBId)).Distinct().Count());
        foreach (var round in detail.Matches.GroupBy(x => (x.Pool, x.Round)))
            Assert.Equal(2 * round.Count(), round.SelectMany(x => new[] { x.TeamAId, x.TeamBId }).Distinct().Count());
        await Assert.ThrowsAsync<ConflictException>(() => Call<ITournamentService>(h, s => s.DrawAsync(id, pools)));
        h.ActAs(h.Alice, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => Call<ITournamentService>(h, s => s.WithdrawAsync(id)));
    }
    [Fact]
    public async Task Tournament_schedule_conflicts_and_results_update_standings_without_creating_court_bookings()
    {
        await using var h = Host();
        h.ActAs(h.Manager, Role.Manager);
        var id = await Call<ITournamentService, int>(h, s => s.CreateAsync(new(h.BranchId, "Giải nội bộ", 3)));
        foreach (var user in new[] { h.Alice, h.Bob, h.Carol })
        {
            h.ActAs(user, Role.Customer);
            await Call<ITournamentService>(h, s => s.RegisterAsync(id, new($"Đội {user}", "Thành viên")));
        }
        h.ActAs(h.Manager, Role.Manager);
        await Call<ITournamentService>(h, s => s.DrawAsync(id, 1));
        var detail = await Call<ITournamentService, TournamentDetail>(h, s => s.DetailAsync(id));
        var matches = detail.Matches;
        var r = new MatchScheduleRequest(h.HanoiNowUtc.AddHours(1), h.HanoiNowUtc.AddHours(2), "Sân A");
        await Call<ITournamentService>(h, s => s.ScheduleAsync(id, matches[0].Id, r));
        await Assert.ThrowsAsync<ConflictException>(() => Call<ITournamentService>(h, s => s.ScheduleAsync(id, matches[1].Id, r with { Location = "Sân B" })));
        await Assert.ThrowsAsync<ConflictException>(() => Call<ITournamentService>(h, s => s.ScoreAsync(id, matches[0].Id, new(21, 15))));
        for (var i = 1; i < matches.Count; i++)
            await Call<ITournamentService>(h, s => s.ScheduleAsync(id, matches[i].Id, r with { StartsAtUtc = r.StartsAtUtc.AddHours(i), EndsAtUtc = r.EndsAtUtc.AddHours(i) }));
        HanoiTime.NowUtc = () => h.HanoiNowUtc.AddHours(5);
        await Assert.ThrowsAsync<BadRequestException>(() => Call<ITournamentService>(h, s => s.ScoreAsync(id, matches[0].Id, new(21, 21))));
        foreach (var match in matches)
            await Call<ITournamentService>(h, s => s.ScoreAsync(id, match.Id, new(21, 15)));
        detail = await Call<ITournamentService, TournamentDetail>(h, s => s.DetailAsync(id));
        Assert.Equal("Completed", detail.Tournament.Status);
        Assert.Equal(3, detail.Teams.Sum(x => x.Won));
        Assert.Equal(6, detail.Teams.Sum(x => x.Played));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.CountAsync()));
    }
    [Fact]
    public async Task Calendar_subscription_is_private_tracks_reschedule_cancel_and_rotates_or_revokes_without_losing_uid()
    {
        await using var h = Host();
        var b = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        await h.MakeConfirmedBookingAsync(h.Bob, h.Court2, h.SlotNoon);
        h.ActAs(h.Alice, Role.Customer);
        var token = await Call<ICalendarSubscriptionService, SubscriptionToken>(h, s => s.RotateAsync());
        var stored = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Set<CalendarSubscription>().SingleAsync());
        Assert.NotEqual(token.Token, stored.TokenHash);
        async Task<string> Feed(string t) => await Call<ICalendarSubscriptionService, string>(h, s => s.FeedAsync(t));
        var feed = await Feed(token.Token);
        Assert.Contains($"UID:booking-{b.Id}@badmintonbooking", feed);
        Assert.Equal(1, feed.Split("BEGIN:VEVENT").Length - 1);
        Assert.Contains("STATUS:CONFIRMED", feed);
        await Call<IBookingAmendmentService>(h, s => s.RescheduleAsync(b.Id, new(h.Court1, h.SlotMorning, h.FutureDate, "Đổi giờ buổi chơi")));
        var moved = await Feed(token.Token);
        Assert.Contains($"DTSTART:{h.FutureDate:yyyyMMdd}T030000Z", moved);
        Assert.Contains($"UID:booking-{b.Id}@badmintonbooking", moved);
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(x => x.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BookingStatus.Cancelled).SetProperty(x => x.CancelledAt, h.HanoiNowUtc.AddHours(1))));
        Assert.Contains("STATUS:CANCELLED", await Feed(token.Token));
        var rotated = await Call<ICalendarSubscriptionService, SubscriptionToken>(h, s => s.RotateAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => Feed(token.Token));
        Assert.Contains("STATUS:CANCELLED", await Feed(rotated.Token));
        await Call<ICalendarSubscriptionService>(h, s => s.RevokeAsync());
        await Assert.ThrowsAsync<NotFoundException>(() => Feed(rotated.Token));
    }
    [Fact]
    public async Task Calendar_stops_serving_when_account_is_locked_and_escapes_untrusted_text()
    {
        await using var h = Host();
        await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Courts.Where(x => x.Id == h.Court1).ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "Sân, A; B\nBEGIN:VEVENT")));
        h.ActAs(h.Alice, Role.Customer);
        var token = await Call<ICalendarSubscriptionService, SubscriptionToken>(h, s => s.RotateAsync());
        var feed = await Call<ICalendarSubscriptionService, string>(h, s => s.FeedAsync(token.Token));
        Assert.Contains("Sân\\, A\\; B\\nBEGIN:VEVENT", feed);
        Assert.Equal(1, feed.Split("\r\nBEGIN:VEVENT\r\n").Length - 1);
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Users.Where(x => x.Id == h.Alice).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, UserStatus.Locked)));
        await Assert.ThrowsAsync<NotFoundException>(() => Call<ICalendarSubscriptionService, string>(h, s => s.FeedAsync(token.Token)));
    }
}
