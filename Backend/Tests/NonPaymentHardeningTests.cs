using System.Security.Claims;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Security;
using Badminton.Core.Store;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Account;
using Contract.Services.Booking;
using Contract.Services.Group;
using Contract.Services.Points;
using Contract.Services.Waitlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelViews.Account;
using ModelViews.Auth;
using ModelViews.Review;
using Repositories.Context;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class NonPaymentHardeningTests
{
    private static AuthService Auth(IServiceProvider sp) => new(sp.GetRequiredService<IUnitOfWork>(), sp.GetRequiredService<ICurrentUser>(),
        new JwtGenerator(Options.Create(new JwtSettings { Key = new string('a', 64), Issuer = "fixture", Audience = "fixture", ExpireMinutes = 10 })), new LoginAttemptTracker());

    private static async Task SetBooking(TestHost h, int id, BookingStatus status, bool checkedIn = false)
        => await h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>(); var b = await db.Bookings.FindAsync(id);
            b!.Status = status; b.CheckedInAt = checkedIn ? DateTime.UtcNow : null; await db.SaveChangesAsync();
        });

    [Fact]
    public async Task Late_profile_update_cannot_restore_revoked_stamp_password_status_or_tier()
    {
        var pause = new ProfilePause();
        await using var h = new TestHost(configure: s => s.AddSingleton<IInterceptor>(pause));
        await h.RunScopeAsync(async sp => { var db = sp.GetRequiredService<BadmintonBookingDbContext>(); var u = await db.Users.FindAsync(h.Alice); u!.SecurityStamp = "old-stamp"; await db.SaveChangesAsync(); });
        h.ActAs(h.Alice, Role.Customer);
        await h.RunScopeAsync(sp => { ((ClaimsIdentity)sp.GetRequiredService<ICurrentUser>().Principal.Identity!).AddClaim(new("sv", "old-stamp")); return Task.CompletedTask; });
        pause.Enabled = true;
        var profile = h.RunScopeAsync(sp => sp.GetRequiredService<IAccountService>().UpdateProfileAsync(h.Alice, new("PROFILE_PAUSED", null, null)));
        await pause.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await h.RunScopeAsync(sp => Auth(sp).RevokeSessionsAsync());
        var stamp = (await h.GetUserAsync(h.Alice)).SecurityStamp;
        await h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            await db.Users.Where(u => u.Id == h.Alice).ExecuteUpdateAsync(s => s.SetProperty(u => u.PasswordHash, "new-hash").SetProperty(u => u.Tier, MembershipTier.Gold));
        });
        pause.Release.TrySetResult(); await profile;
        var user = await h.GetUserAsync(h.Alice);
        Assert.NotEqual("old-stamp", stamp); Assert.Equal(stamp, user.SecurityStamp);
        Assert.Equal("new-hash", user.PasswordHash); Assert.Equal(MembershipTier.Gold, user.Tier);
    }

    [Fact]
    public async Task Completion_requires_end_and_presence_and_does_not_release_future_slot()
    {
        await using var h = new TestHost();
        var b = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        await SetBooking(h, b.Id, BookingStatus.Confirmed, true); h.ActAs(h.Manager, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().MarkCompletedAsync(b.Id, h.Manager)));
        await Assert.ThrowsAsync<ConflictException>(() => h.BookAsync(h.Bob, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd")));
        Assert.Equal(0, await h.CountPointRowsAsync(b.Id));
        await SetBooking(h, b.Id, BookingStatus.Confirmed);
        HanoiTime.NowUtc = () => h.HanoiNowUtc.AddHours(5);
        Assert.Null(await h.RunScopeAsync(sp => sp.GetRequiredService<BookingCompletionService>().CompleteAsync(b.Id, true)));
        await SetBooking(h, b.Id, BookingStatus.Confirmed, true);
        var result = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().MarkCompletedAsync(b.Id, h.Manager));
        Assert.Equal(b.PayableAmount / 1000, result.PointsEarned);
    }

    [Fact]
    public async Task Checkin_window_ttl_and_no_show_are_guarded_in_service_and_atomic_commands()
    {
        await using var h = new TestHost();
        var b = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        h.ActAs(h.Manager, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<ICheckinService>().CheckinByCodeAsync(b.BookingCode, h.Manager)));
        HanoiTime.NowUtc = () => h.HanoiNowUtc.AddHours(2.5);
        await h.RunScopeAsync(async sp => { var db = sp.GetRequiredService<BadmintonBookingDbContext>(); await db.Bookings.Where(x => x.Id == b.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.PendingExpiresAt, DateTime.UtcNow.AddSeconds(-1))); });
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<ICheckinService>().CheckinByCodeAsync(b.BookingCode, h.Manager)));
        await SetBooking(h, b.Id, BookingStatus.Confirmed);
        await h.RunScopeAsync(sp => sp.GetRequiredService<ICheckinService>().CheckinByCodeAsync(b.BookingCode, h.Manager));
        HanoiTime.NowUtc = () => h.HanoiNowUtc.AddHours(3);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().MarkNoShowAsync(b.Id, h.Manager)));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IUnitOfWork>().Bookings.MarkNoShowAsync(b.Id, "race", DateTime.UtcNow, HanoiTime.HanoiNow())));
        Assert.Equal(0, (await h.GetUserAsync(h.Alice)).NoShowCount);
    }

    [Fact]
    public async Task Locked_waiter_does_not_block_next_and_stale_waiting_rows_expire()
    {
        await using var h = new TestHost();
        var b = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        var first = await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().JoinAsync(h.Bob, h.Court1, h.SlotNoon, h.FutureDate, 1, null));
        var second = await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().JoinAsync(h.Carol, h.Court1, h.SlotNoon, h.FutureDate, 1, null));
        await SetBooking(h, b.Id, BookingStatus.Cancelled);
        await h.RunScopeAsync(async sp => { var db = sp.GetRequiredService<BadmintonBookingDbContext>(); await db.Users.Where(u => u.Id == h.Bob).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, UserStatus.Locked)); });
        var result = await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().PromoteAsync(h.Court1, h.SlotNoon, h.FutureDate));
        Assert.Equal(h.Carol, result!.UserId);
        HanoiTime.NowUtc = () => h.HanoiNowUtc.AddDays(1);
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().ExpirePastEntriesAsync()));
        var waiting = await h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().GetMineAsync(h.Bob));
        Assert.Equal(WaitlistStatus.Expired, waiting.Single(w => w.Id == first.Id).Status);
    }

    [Fact]
    public async Task Parallel_reviews_keep_exact_aggregate_and_reject_duplicates()
    {
        await using var h = new TestHost();
        var a = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        var b = await h.BookAsync(h.Bob, h.Court1, h.SlotPeak, h.FutureDate.ToString("yyyy-MM-dd"));
        await SetBooking(h, a.Id, BookingStatus.Completed); await SetBooking(h, b.Id, BookingStatus.Completed);
        await Task.WhenAll(h.RunScopeAsync(sp => ActivatorUtilities.CreateInstance<ReviewService>(sp).CreateAsync(new(a.Id, 5, "a"), h.Alice)),
            h.RunScopeAsync(sp => ActivatorUtilities.CreateInstance<ReviewService>(sp).CreateAsync(new(b.Id, 1, "b"), h.Bob)));
        var court = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Courts.AsNoTracking().SingleAsync(c => c.Id == h.Court1));
        Assert.Equal(2, court.RatingCount); Assert.Equal(3d, court.RatingAvg);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => ActivatorUtilities.CreateInstance<ReviewService>(sp).CreateAsync(new(a.Id, 5, "again"), h.Alice)));
    }

    [Fact]
    public async Task Points_summary_handles_large_values_without_overflow()
    {
        await using var h = new TestHost(); await h.CountPointsIntoAsync(h.Alice, 150_000, "legacy fixture");
        var result = await h.RunScopeAsync(sp => sp.GetRequiredService<IAccountService>().GetPointsAsync(h.Alice));
        Assert.Equal(3_000_000L, result.RedeemableVnd);
    }

    [Fact]
    public async Task Group_invites_rotation_member_lifecycle_and_owner_guards()
    {
        await using var h = new TestHost(); h.ActAs(h.Alice, Role.Customer);
        var g = await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().CreateAsync("Team", h.Alice));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(g.Id, h.Bob)));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(g.Id, h.Bob, g.InviteCode));
        Assert.Single(await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().GetJoinedAsync(h.Bob)));
        var replacement = await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().RotateInviteAsync(g.Id, h.Alice));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(g.Id, h.Carol, g.InviteCode)));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().JoinAsync(g.Id, h.Carol, replacement));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().DisbandAsync(g.Id, h.Bob)));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().TransferAsync(g.Id, h.Bob, h.Alice));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().LeaveAsync(g.Id, h.Alice));
        Assert.False(await h.RunScopeAsync(sp => sp.GetRequiredService<IGroupService>().CanBookForGroupAsync(g.Id, h.Alice)));
    }

    [Fact]
    public async Task Recovery_code_is_admin_verified_single_use_and_revokes_old_sessions()
    {
        await using var h = new TestHost(); const string adminPassword = "SyntheticAdminPassword123";
        var adminId = await h.RunScopeAsync(async sp => { var db = sp.GetRequiredService<BadmintonBookingDbContext>(); var admin = await db.Users.SingleAsync(u => u.Role == Role.Admin); admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword); await db.SaveChangesAsync(); return admin.Id; });
        h.ActAs(adminId, Role.Admin);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => Auth(sp).IssueRecoveryCodeAsync(h.Alice, new("wrong", "identity verified"))));
        var code = await h.RunScopeAsync(sp => Auth(sp).IssueRecoveryCodeAsync(h.Alice, new(adminPassword, "identity verified at desk")));
        var oldStamp = (await h.GetUserAsync(h.Alice)).SecurityStamp;
        await h.RunScopeAsync(sp => Auth(sp).ResetPasswordAsync(new("0900000001", code.Code, "NewCustomerPassword123")));
        await Assert.ThrowsAsync<BadRequestException>(() => h.RunScopeAsync(sp => Auth(sp).ResetPasswordAsync(new("0900000001", code.Code, "AnotherPassword123"))));
        var user = await h.GetUserAsync(h.Alice);
        Assert.NotEqual(oldStamp, user.SecurityStamp); Assert.Null(user.PasswordRecoveryHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewCustomerPassword123", user.PasswordHash));
    }

    [Fact]
    public async Task Admin_search_filters_before_paging_and_branch_changes_protect_bookings()
    {
        await using var h = new TestHost();
        var b = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        h.ActAs(h.Manager, Role.Manager);
        var found = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().GetAllPagedAsync(null, null, new Page(1, 1), "0900000001", h.FutureDate));
        Assert.Equal(1, found.TotalCount); Assert.Equal(b.Id, found.Items.Single().Id);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => ActivatorUtilities.CreateInstance<BranchService>(sp)
            .UpdateAsync(h.BranchId, new(null, null, false, null, null), h.Manager)));
        Assert.True(await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Branches.Where(x => x.Id == h.BranchId).Select(x => x.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task Capacity_cannot_shrink_below_existing_active_booking_size()
    {
        await using var h = new TestHost();
        var b = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(x => x.Id == b.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PlayerCount, 2)));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => ActivatorUtilities.CreateInstance<CourtService>(sp)
            .UpdateAsync(h.Court1, new(null, null, null, null, 1, null, null, null), h.Manager)));
        var capacity = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Courts.Where(x => x.Id == h.Court1).Select(x => x.Capacity).SingleAsync());
        Assert.Equal(2, capacity);
    }

    [Fact]
    public async Task Legacy_clawback_uses_converted_units_but_new_awards_are_not_divided_twice()
    {
        await using var h = new TestHost();
        var old = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        var fresh = await h.BookAsync(h.Alice, h.Court2, h.SlotPeak, h.FutureDate.ToString("yyyy-MM-dd"));
        await h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            db.PointLedger.AddRange(new PointLedger { UserId = h.Alice, BookingId = old.Id, Delta = 100000, LedgerKey = $"earn:{old.Id}", Reason = "legacy", CreatedAt = DateTime.UtcNow.AddDays(-1) },
                new PointLedger { UserId = h.Alice, Delta = -99900, LedgerKey = $"unit-v2:{h.Alice}", Reason = "conversion", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });
        await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().ClawbackAsync(h.Alice, old.Id, old.PayableAmount / 2, "half"));
        Assert.Equal(50, await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Alice)));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().AwardAsync(h.Alice, fresh.Id, 100000, "new units"));
        Assert.Equal(150, await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Alice)));
    }

    [Fact]
    public async Task Audit_failure_rolls_back_court_configuration()
    {
        await using var h = new TestHost(configure: s => s.AddSingleton<IInterceptor>(new RejectCourtAudit()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.RunScopeAsync(sp => ActivatorUtilities.CreateInstance<CourtService>(sp)
            .UpdateAsync(h.Court1, new("Must roll back", null, null, null, null, null, null, null), h.Manager)));
        var name = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Courts.Where(c => c.Id == h.Court1).Select(c => c.Name).SingleAsync());
        Assert.Equal("Court 1", name);
    }

    [Fact]
    public async Task Missing_attendance_is_actionable_in_summary_and_server_filter()
    {
        await using var h = new TestHost();
        var absent = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        var present = await h.BookAsync(h.Bob, h.Court2, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        await SetBooking(h, absent.Id, BookingStatus.Confirmed); await SetBooking(h, present.Id, BookingStatus.Confirmed, true);
        h.ActAs(h.Manager, Role.Manager); HanoiTime.NowUtc = () => h.HanoiNowUtc.AddHours(5);
        var summary = await h.RunScopeAsync(sp => ActivatorUtilities.CreateInstance<ReportService>(sp).GetSummaryAsync());
        Assert.Equal(1, summary.AttendanceToReview);
        var list = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().GetAllPagedAsync(null, null, new Page(1, 10), needsAttendance: true));
        Assert.Equal(absent.Id, list.Items.Single().Id);
    }

    private sealed class RejectCourtAudit : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<AuditLog>().Any(e => e.State == EntityState.Added && e.Entity.Action == "court.update"))
                throw new InvalidOperationException("Synthetic audit persistence failure");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ProfilePause : SaveChangesInterceptor
    {
        public bool Enabled;
        public readonly TaskCompletionSource Arrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<User>().Any(e => e.State == EntityState.Modified && e.Entity.FullName == "PROFILE_PAUSED")) {
                Arrived.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), ct);
            }
            return result;
        }
    }
}
