using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Policies;
using Badminton.Core.Time;
using Contract.Repositories.Entity;
using Contract.Services.Operations;
using Contract.Services.Group;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ModelViews.Operations;
using Repositories.Context;
using Xunit;
namespace Badminton.Tests;

public sealed class ClubHardeningTests
{
    [Fact]
    public async Task Hardening_migration_protects_audit_data_and_new_Postgres_tables_use_RLS()
    {
        await using var h = new TestHost(useMigrations: true);
        await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            if (db.Database.IsNpgsql())
                Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_class WHERE relrowsecurity AND relname IN ('EquipmentConditionReports','TournamentScoreRevisions')").SingleAsync());
            var item = new EquipmentItem { BranchId = h.BranchId, Name = "Audit fixture", Stock = 0, Rental = true };
            db.Add(item);
            await db.SaveChangesAsync();
            var order = new EquipmentOrder { ItemId = item.Id, UserId = h.Alice, Quantity = 1, Rental = true, RequestKey = "audit-sentinel", Status = "Lost", DueAtUtc = DateTime.UtcNow };
            db.Add(order);
            await db.SaveChangesAsync();
            db.Add(new EquipmentConditionReport { OrderId = order.Id, ActorId = h.Manager, UnusableQuantity = 1, Lost = true, Reason = "Immutable migration sentinel" });
            await db.SaveChangesAsync();
            var previous = db.Database.GetMigrations().Last(x => x.EndsWith("_AddClubFeatures"));
            await Assert.ThrowsAnyAsync<Exception>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            Assert.Equal("Immutable migration sentinel", (await db.Set<EquipmentConditionReport>().SingleAsync()).Reason);
        });
    }
    private sealed class Clock : IBusinessClock
    {
        public DateTime Now; public DateTime UtcNow => Now; public DateTime LocalNow => Now.AddHours(7); public DateOnly Today => DateOnly.FromDateTime(LocalNow);
    }
    private static Task<T> Call<S, T>(TestHost h, Func<S, Task<T>> f) where S : notnull => h.RunScopeAsync(sp => f(sp.GetRequiredService<S>()));
    private static Task Call<S>(TestHost h, Func<S, Task> f) where S : notnull => h.RunScopeAsync(sp => f(sp.GetRequiredService<S>()));

    [Theory]
    [InlineData(0, 1, 2, 0, 0)]
    [InlineData(100001, 1, 2, 33334, 66667)]
    [InlineData(1, 0, 10, 0, 1)]
    [InlineData(100001, 1, 1, 50001, 50000)]
    public void Allocation_preserves_exact_vnd_and_uses_largest_remainder(int total, int a, int b, int ea, int eb)
    {
        var result = GroupCostAllocation.Allocate(total, new Dictionary<int, int> { [2] = b, [1] = a });
        Assert.Equal(total, result.Values.Sum());
        Assert.Equal(ea, result[1]);
        Assert.Equal(eb, result[2]);
    }
    [Fact]
    public async Task Worker_closes_past_sessions_expires_holds_and_reminds_overdue_without_releasing_issued_stock()
    {
        var clock = new Clock();
        await using var h = new TestHost(configure: s => s.AddSingleton<IBusinessClock>(clock));
        clock.Now = h.HanoiNowUtc;
        h.ActAs(h.Alice, Role.Customer);
        var session = await Call<IPlayerMatchingService, int>(h, s => s.CreateAsync(new(h.BranchId, "Thủ Đức", 2, clock.Now.AddMinutes(1), clock.Now.AddMinutes(5), 2, "Test tự đóng")));
        h.ActAs(h.Manager, Role.Manager);
        var item = await Call<IEquipmentService, int>(h, s => s.SaveItemAsync(null, new(h.BranchId, "Worker racket", true, 2, 20000, true)));
        h.ActAs(h.Alice, Role.Customer);
        var first = await Call<IEquipmentService, int>(h, s => s.ReserveAsync(new(item, 1, clock.Now.AddHours(2), "expires")));
        var issued = await Call<IEquipmentService, int>(h, s => s.ReserveAsync(new(item, 1, clock.Now.AddHours(2), "issued")));
        h.ActAs(h.Manager, Role.Manager);
        await Call<IEquipmentService>(h, s => s.TransitionAsync(issued, "issue"));
        clock.Now = clock.Now.AddMinutes(31);
        Assert.Equal(1, Assert.Single(await Call<IEquipmentService, IReadOnlyList<EquipmentItemView>>(h, s => s.ItemsAsync(h.BranchId))).Available);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IEquipmentService>(h, s => s.TransitionAsync(first, "issue")));
        Assert.Equal(2, await Call<IClubMaintenanceService, int>(h, s => s.RunAsync()));
        Assert.Equal(0, await Call<IClubMaintenanceService, int>(h, s => s.RunAsync()));
        clock.Now = clock.Now.AddHours(2);
        await Call<IClubMaintenanceService, int>(h, s => s.RunAsync());
        await Call<IClubMaintenanceService, int>(h, s => s.RunAsync());
        var orders = await Call<IEquipmentService, IReadOnlyList<EquipmentOrderView>>(h, s => s.OrdersAsync(true, h.BranchId, 1));
        Assert.Equal("Expired", orders.Single(x => x.Id == first).Status);
        Assert.True(orders.Single(x => x.Id == issued).Overdue);
        Assert.Equal(1, Assert.Single(await Call<IEquipmentService, IReadOnlyList<EquipmentItemView>>(h, s => s.ItemsAsync(h.BranchId))).Available);
        Assert.True(await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Set<PlayerSession>().Where(x => x.Id == session).Select(x => x.Closed).SingleAsync()));
        Assert.Equal(2, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Set<InboxMessage>().CountAsync(x => x.UserId == h.Alice && x.EventKey.StartsWith("equipment-"))));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Damage_or_loss_is_private_immutable_idempotent_and_reduces_stock_once(bool lost)
    {
        var clock = new Clock();
        await using var h = new TestHost(configure: s => s.AddSingleton<IBusinessClock>(clock));
        clock.Now = h.HanoiNowUtc;
        h.ActAs(h.Manager, Role.Manager);
        var item = await Call<IEquipmentService, int>(h, s => s.SaveItemAsync(null, new(h.BranchId, "Biên bản", true, 3, 20000, true)));
        h.ActAs(h.Alice, Role.Customer);
        var order = await Call<IEquipmentService, int>(h, s => s.ReserveAsync(new(item, 2, clock.Now.AddHours(1), "condition")));
        h.ActAs(h.Manager, Role.Manager);
        await Call<IEquipmentService>(h, s => s.TransitionAsync(order, "issue"));
        var report = new EquipmentConditionRequest(1, lost, 50000, "Khách xác nhận tình trạng tại quầy");
        h.ActAs(h.ManagerOtherBranch, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IEquipmentService>(h, s => s.RecordConditionAsync(order, report)));
        h.ActAs(h.Manager, Role.Manager);
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Call<IEquipmentService>(h, s => s.RecordConditionAsync(order, report))));
        var inventory = Assert.Single(await Call<IEquipmentService, IReadOnlyList<EquipmentItemView>>(h, s => s.ItemsAsync(h.BranchId)));
        Assert.Equal(2, inventory.Stock);
        Assert.Equal(2, inventory.Available);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IEquipmentService>(h, s => s.RecordConditionAsync(order, report with { AssessedAmount = 1 })));
        h.ActAs(h.Bob, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IEquipmentService, EquipmentConditionView?>(h, s => s.ConditionAsync(order)));
        h.ActAs(h.Alice, Role.Customer);
        Assert.Equal(50000, (await Call<IEquipmentService, EquipmentConditionView?>(h, s => s.ConditionAsync(order)))!.AssessedAmount);
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.CountAsync()));
    }
    [Fact]
    public async Task Attendance_split_rejects_invalid_minutes_and_replays_only_the_same_recorded_participation()
    {
        await using var h = new TestHost();
        h.ActAs(h.Alice, Role.Customer);
        var group = await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().CreateAsync("Theo giờ tham dự", h.Alice));
        h.ActAs(h.Bob, Role.Customer);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(group.Id, h.Bob, group.InviteCode));
        var booking = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(x => x.Id == booking.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.GroupId, group.Id).SetProperty(x => x.PayableAmount, 100001)));
        h.ActAs(h.Alice, Role.Customer);
        var request = new AttendanceSplitRequest([new(h.Alice, 30), new(h.Bob, 60)]);
        await Assert.ThrowsAsync<BadRequestException>(() => Call<IGroupExpenseService>(h, s => s.CreateByAttendanceAsync(booking.Id, new([null!]))));
        await Assert.ThrowsAsync<BadRequestException>(() => Call<IGroupExpenseService>(h, s => s.CreateByAttendanceAsync(booking.Id, new([new(h.Alice, 61)]))));
        await Call<IGroupExpenseService>(h, s => s.CreateByAttendanceAsync(booking.Id, request));
        await Call<IGroupExpenseService>(h, s => s.CreateByAttendanceAsync(booking.Id, request));
        var result = (await Call<IGroupExpenseService, ExpenseView?>(h, s => s.GetAsync(booking.Id)))!;
        Assert.Equal("Attendance", result.SplitMethod);
        Assert.Equal(100001, result.Shares.Sum(x => x.Amount));
        Assert.Equal(33334, result.Shares.Single(x => x.UserId == h.Alice).Amount);
        await Assert.ThrowsAsync<ConflictException>(() => Call<IGroupExpenseService>(h, s => s.CreateByAttendanceAsync(booking.Id, new([new(h.Alice, 60), new(h.Bob, 60)]))));
        await Assert.ThrowsAsync<ConflictException>(() => Call<IGroupExpenseService>(h, s => s.CreateAsync(booking.Id, [h.Alice, h.Bob])));
    }
    [Fact]
    public async Task Class_requires_a_confirmed_court_and_compensates_once_when_backing_booking_changes()
    {
        var clock = new Clock();
        await using var h = new TestHost(configure: s => s.AddSingleton<IBusinessClock>(clock));
        clock.Now = h.HanoiNowUtc;
        h.ActAs(h.Manager, Role.Manager);
        var coach = await Call<ICoachingService, int>(h, s => s.SaveCoachAsync(null, new(h.BranchId, "Court coach", "Lớp có sân", true)));
        var slot = await Call<ICoachingService, int>(h, s => s.AddSlotAsync(coach, new(clock.Now.AddHours(1), clock.Now.AddHours(2), 2, 100000)));
        h.ActAs(h.Alice, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => Call<ICoachingService, int>(h, s => s.ReserveAsync(new(slot, 1, "draft"))));
        h.ActAs(h.Manager, Role.Manager);
        var booking = await ClubCourtFixture.AttachAsync(h, slot);
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(x => x.Id == booking).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, h.Bob)));
        await Assert.ThrowsAsync<ConflictException>(() => Call<ICoachingService>(h, s => s.AttachCourtAsync(slot, booking)));
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(x => x.Id == booking).ExecuteUpdateAsync(s => s.SetProperty(x => x.UserId, h.Manager)));
        h.ActAs(h.Alice, Role.Customer);
        await Call<ICoachingService, int>(h, s => s.ReserveAsync(new(slot, 1, "valid")));
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(x => x.Id == booking).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, BookingStatus.Cancelled)));
        Assert.Equal(0, Assert.Single(await Call<ICoachingService, IReadOnlyList<CoachSlotView>>(h, s => s.SlotsAsync(coach, clock.Now, clock.Now.AddDays(1)))).Available);
        await Assert.ThrowsAsync<ConflictException>(() => Call<ICoachingService, int>(h, s => s.ReserveAsync(new(slot, 1, "invalid"))));
        Assert.Equal(1, await Call<IClubMaintenanceService, int>(h, s => s.RunAsync()));
        Assert.Equal(0, await Call<IClubMaintenanceService, int>(h, s => s.RunAsync()));
        Assert.Equal("Cancelled", Assert.Single(await Call<ICoachingService, IReadOnlyList<CoachingView>>(h, s => s.ReservationsAsync(false, null, 1))).Status);
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Set<InboxMessage>().CountAsync(x => x.UserId == h.Alice && x.EventKey.StartsWith("coach-court-cancelled:"))));
    }
    [Fact]
    public async Task Score_corrections_need_reason_and_current_version_and_keep_an_immutable_history()
    {
        var clock = new Clock();
        await using var h = new TestHost(configure: s => s.AddSingleton<IBusinessClock>(clock));
        clock.Now = h.HanoiNowUtc;
        h.ActAs(h.Manager, Role.Manager);
        var id = await Call<ITournamentService, int>(h, s => s.CreateAsync(new(h.BranchId, "Audit score", 2)));
        h.ActAs(h.Alice, Role.Customer);
        await Call<ITournamentService>(h, s => s.RegisterAsync(id, new("A", "Alice")));
        h.ActAs(h.Bob, Role.Customer);
        await Call<ITournamentService>(h, s => s.RegisterAsync(id, new("B", "Bob")));
        h.ActAs(h.Manager, Role.Manager);
        await Call<ITournamentService>(h, s => s.DrawAsync(id, 1));
        var match = Assert.Single((await Call<ITournamentService, TournamentDetail>(h, s => s.DetailAsync(id))).Matches);
        await Call<ITournamentService>(h, s => s.ScheduleAsync(id, match.Id, new(clock.Now.AddHours(1), clock.Now.AddHours(2), "Sân A")));
        clock.Now = clock.Now.AddHours(3);
        await Call<ITournamentService>(h, s => s.ScoreAsync(id, match.Id, new(21, 15)));
        await Assert.ThrowsAsync<BadRequestException>(() => Call<ITournamentService>(h, s => s.ScoreAsync(id, match.Id, new(15, 21, "", 1))));
        await Call<ITournamentService>(h, s => s.ScoreAsync(id, match.Id, new(15, 21, "Đối chiếu biên bản giấy", 1)));
        await Assert.ThrowsAsync<ConflictException>(() => Call<ITournamentService>(h, s => s.ScoreAsync(id, match.Id, new(21, 17, "Ghi đè từ màn hình cũ", 1))));
        var history = await Call<ITournamentService, IReadOnlyList<ScoreRevisionView>>(h, s => s.ScoreHistoryAsync(id, match.Id));
        Assert.Equal(2, history.Count);
        Assert.Equal(21, history[1].OldScoreA);
        Assert.Equal(15, history[1].ScoreA);
        Assert.Equal(h.Manager, history[1].ActorId);
    }
    [Fact]
    public async Task Calendar_status_distinguishes_created_link_from_a_successful_feed_read()
    {
        var clock = new Clock();
        await using var h = new TestHost(configure: s => s.AddSingleton<IBusinessClock>(clock));
        clock.Now = h.HanoiNowUtc;
        h.ActAs(h.Alice, Role.Customer);
        var token = await Call<ICalendarSubscriptionService, SubscriptionToken>(h, s => s.RotateAsync());
        Assert.Null((await Call<ICalendarSubscriptionService, SubscriptionState>(h, s => s.StateAsync())).LastFetchedAtUtc);
        await Call<ICalendarSubscriptionService, string>(h, s => s.FeedAsync(token.Token));
        Assert.Equal(clock.Now, (await Call<ICalendarSubscriptionService, SubscriptionState>(h, s => s.StateAsync())).LastFetchedAtUtc);
        await Call<ICalendarSubscriptionService, SubscriptionToken>(h, s => s.RotateAsync());
        Assert.Null((await Call<ICalendarSubscriptionService, SubscriptionState>(h, s => s.StateAsync())).LastFetchedAtUtc);
    }
}
