using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ModelViews.Operations;
using Npgsql;
using Repositories.Context;
using Xunit;

namespace Badminton.Tests;

public sealed class BookingExperienceSafetyTests
{
    [Fact]
    public async Task Old_branch_snapshot_cannot_check_in_or_confirm_after_cross_branch_reschedule()
    {
        await using var h = new TestHost(); var b = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        var otherCourt = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Courts.Where(c => c.BranchId == h.BranchId2).Select(c => c.Id).SingleAsync());
        h.ActAs(h.Alice, Role.Customer);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingAmendmentService>().RescheduleAsync(b.Id, new(otherCourt, h.SlotNoon, h.FutureDate, "Khách đổi lịch cùng giá sang cơ sở khác")));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IUnitOfWork>().Bookings.CheckinAsync(b.Id, DateTime.UtcNow, h.HanoiNowUtc.AddHours(10), h.BranchId)));
        var pending = await h.BookAsync(h.Alice, h.Court1, h.SlotMorning, h.FutureDate.ToString("yyyy-MM-dd"));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingAmendmentService>().RescheduleAsync(pending.Id, new(otherCourt, h.SlotMorning, h.FutureDate, "Khách đổi lịch cùng giá sang cơ sở khác")));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IUnitOfWork>().Bookings.ConfirmAsync(pending.Id, "test-token", DateTime.UtcNow, h.BranchId)));
        Assert.Equal(BookingStatus.Pending, (await h.GetBookingAsync(pending.Id)).Status);
        Assert.Equal(h.BranchId2, (await h.GetBookingAsync(b.Id)).BranchId);
    }

    [Fact]
    public async Task Concurrent_move_and_booking_claim_only_one_target_and_keep_source_on_failure()
    {
        await using var h = new TestHost(); var b = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon); h.ActAs(h.Alice, Role.Customer);
        async Task<bool> Move() { try { await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingAmendmentService>().RescheduleAsync(b.Id, new(h.Court2, h.SlotNoon, h.FutureDate, "Khách yêu cầu đổi sân cùng khung giờ"))); return true; } catch (ConflictException) { return false; } }
        async Task<bool> Book() { try { await h.BookAsync(h.Bob, h.Court2, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd")); return true; } catch (ConflictException) { return false; } }
        var result = await Task.WhenAll(Move(), Book()); Assert.Single(result, success => success);
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.CountAsync(x => x.CourtId == h.Court2 && x.Date == h.FutureDate && x.SlotId == h.SlotNoon && (x.Status == BookingStatus.Pending || x.Status == BookingStatus.Confirmed))));
        Assert.Equal(result[0] ? h.Court2 : h.Court1, (await h.GetBookingAsync(b.Id)).CourtId);
        Assert.Equal(b.PayableAmount, (await h.GetBookingAsync(b.Id)).PayableAmount);
    }

    [Fact]
    public async Task Changed_batch_quote_rolls_back_before_any_reservation_or_manifest_is_committed()
    {
        await using var h = new TestHost(); h.ActAs(h.Alice, Role.Customer);
        var request = new BatchBookingRequest(h.Court1, h.FutureDate, [h.SlotNoon], 1, 1, null, "quote-mismatch", 1);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBatchBookingService>().CreateAsync(request)));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.CountAsync()));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Set<BookingBatch>().CountAsync()));
        Assert.Single(await h.RunScopeAsync(sp => sp.GetRequiredService<IBatchBookingService>().CreateAsync(request with { ExpectedTotal = null })));
    }

    [Fact]
    public async Task Experience_migration_upgrades_existing_group_booking_and_guards_destructive_downgrade()
    {
        await using var h = new TestHost(useMigrations: true);
        await h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>(); var migrations = db.Database.GetMigrations().ToArray(); var newest = Array.FindIndex(migrations, name => name.EndsWith("_AddBookingExperience")); Assert.True(newest > 0);
            await db.GetService<IMigrator>().MigrateAsync(migrations[newest - 1]);
            var group = new PlayGroup { Name = "Legacy group", OwnerId = h.Alice, MaxMembers = 5 }; group.Members.Add(new() { UserId = h.Alice }); db.PlayGroups.Add(group); await db.SaveChangesAsync();
            db.Bookings.Add(new Booking { BookingCode = "LEGACY-GROUP", UserId = h.Alice, GroupId = group.Id, CourtId = h.Court1, BranchId = h.BranchId, SlotId = h.SlotNoon, Date = h.FutureDate, Status = BookingStatus.Confirmed, PlayerCount = 1, PayableAmount = 100000, TotalAmount = 100000 }); await db.SaveChangesAsync();
            await db.GetService<IMigrator>().MigrateAsync();
            var rsvp = await db.Set<GroupRsvp>().SingleAsync(); Assert.Equal(h.Alice, rsvp.UserId); Assert.True(rsvp.Attending);
            await Assert.ThrowsAnyAsync<Exception>(() => db.GetService<IMigrator>().MigrateAsync(migrations[newest - 1]));
            Assert.Equal(1, await db.Set<GroupRsvp>().CountAsync()); Assert.Equal("LEGACY-GROUP", (await db.Bookings.SingleAsync()).BookingCode);
        });
    }

    [Fact]
    public async Task PostgreSQL_experience_tables_have_rls_and_no_Data_API_grants()
    {
        if (Environment.GetEnvironmentVariable("BADMINTON_TEST_POSTGRES") is null) return;
        await using var h = new TestHost(useMigrations: true); await using var connection = new NpgsqlConnection(h.ConnectionString); await connection.OpenAsync();
        await new NpgsqlCommand("DO $roles$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='anon') THEN CREATE ROLE anon NOLOGIN; END IF; IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='authenticated') THEN CREATE ROLE authenticated NOLOGIN; END IF; END $roles$;", connection).ExecuteNonQueryAsync();
        string[] tables = ["BookingBatches", "BookingCases", "BookingHistory", "CourtFavorites", "DisruptionContacts", "GroupRsvps", "HandoverItems", "InboxMessages", "NotificationPreferences", "PrivacyRequests", "ReviewReplies", "ReviewReports", "StaffShifts", "StoredMedia"];
        foreach (var table in tables) {
            await using var command = new NpgsqlCommand("SELECT relrowsecurity AND NOT has_table_privilege('anon',oid,'SELECT') AND NOT has_table_privilege('authenticated',oid,'SELECT') FROM pg_class WHERE oid=to_regclass(@name)", connection); command.Parameters.AddWithValue("name", "public.\"" + table + "\""); Assert.True((bool)(await command.ExecuteScalarAsync())!);
        }
        await using var transaction = await connection.BeginTransactionAsync();
        await new NpgsqlCommand("SET LOCAL ROLE anon", connection, transaction).ExecuteNonQueryAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => new NpgsqlCommand("SELECT 1 FROM \"BookingCases\"", connection, transaction).ExecuteNonQueryAsync());
        Assert.Equal("42501", error.SqlState); await transaction.RollbackAsync();
    }

    [Fact]
    public void Page_boundaries_cannot_overflow_the_SQL_offset()
    {
        Assert.True(new Page(int.MaxValue, 30).Skip >= 0); Assert.Equal(0, new Page(0, 0).Skip); Assert.Equal(0, new Page(int.MaxValue, int.MaxValue).Skip);
    }
}
