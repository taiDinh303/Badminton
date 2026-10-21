using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Services.Account;
using Contract.Services.Booking;
using Contract.Services.Maintenance;
using Contract.Services.Payment;
using Contract.Services.Voucher;
using Contract.Services.Waitlist;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using ModelViews.Account;
using Repositories.Context;
using Xunit;

namespace Badminton.Tests;

public sealed class RequestedFeatureRegressionTests : IAsyncLifetime
{
    private readonly TestHost _h = new();
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _h.DisposeAsync();

    [Fact]
    public async Task Maintenance_refunds_full_inside_one_hour_and_blocks_only_overlapping_slots()
    {
        var booking = await _h.MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotSoon40);
        _h.ActAs(_h.Manager, Role.Manager);
        await Set(CourtDateStatus.Maintenance, TimeSpan.FromHours(9.5), TimeSpan.FromHours(10));
        Assert.Equal(BookingStatus.Refunded, (await _h.GetBookingAsync(booking.Id)).Status);
        var payments = await _h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().GetByBookingAsync(booking.Id, _h.Manager));
        Assert.Equal(booking.PayableAmount, payments.Single(p => p.RefundAmount > 0).RefundAmount);
        await Set(CourtDateStatus.Maintenance, TimeSpan.FromHours(9.5), TimeSpan.FromHours(10));
        Assert.Equal(1, await _h.CountRefundRowsAsync(booking.Id));
        var grid = await _h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().GetAvailabilityAsync(_h.BranchId, _h.FutureDate));
        var court = grid.Single(c => c.CourtId == _h.Court1);
        Assert.Equal("Bảo trì", court.Slots.Single(s => s.SlotId == _h.SlotSoon40).LockReason);
        Assert.False(court.Slots.Single(s => s.SlotId == _h.SlotNoon).IsLocked);
        await Assert.ThrowsAsync<ConflictException>(() => _h.BookAsync(_h.Bob, _h.Court1, _h.SlotSoon40, _h.FutureDate.ToString("yyyy-MM-dd")));
        await Set(CourtDateStatus.Open);
        await _h.BookAsync(_h.Bob, _h.Court1, _h.SlotSoon40, _h.FutureDate.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task Maintenance_cancels_pending_and_restores_redeemed_points()
    {
        await _h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            db.PointLedger.Add(new Contract.Repositories.Entity.PointLedger { UserId = _h.Alice, Delta = 2000, Reason = "Test credit", LedgerKey = "test-credit" });
            await db.SaveChangesAsync();
        });
        var b = await _h.BookAsync(_h.Alice, _h.Court1, _h.SlotNoon, _h.FutureDate.ToString("yyyy-MM-dd"), 1000);
        _h.ActAs(_h.Manager, Role.Manager);
        await Set(CourtDateStatus.Closed);
        Assert.Equal(BookingStatus.Cancelled, (await _h.GetBookingAsync(b.Id)).Status);
        var points = await _h.RunScopeAsync(sp => sp.GetRequiredService<IAccountService>().GetPointsAsync(_h.Alice));
        Assert.Equal(2000, points.Balance);
    }

    [Fact]
    public async Task Maintenance_denies_other_branch_and_invalid_time_range()
    {
        _h.ActAs(_h.ManagerOtherBranch, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => Set(CourtDateStatus.Closed, actorId: _h.ManagerOtherBranch));
        _h.ActAs(_h.Manager, Role.Manager);
        await Assert.ThrowsAsync<BadRequestException>(() => Set(CourtDateStatus.Closed, TimeSpan.FromHours(15), TimeSpan.FromHours(14)));
        await Assert.ThrowsAsync<BadRequestException>(() => Set(CourtDateStatus.Closed, TimeSpan.FromHours(14)));
    }

    [Fact]
    public async Task Profile_updates_fields_and_reports_membership()
    {
        _h.ActAs(_h.Alice, Role.Customer);
        var updated = await _h.RunScopeAsync(sp => sp.GetRequiredService<IAccountService>().UpdateProfileAsync(_h.Alice,
            new UpdateProfileRequest("Alice Updated", "alice@example.test", "https://example.test/avatar.png")));
        Assert.Equal("Alice Updated", updated.FullName);
        Assert.Equal("None", updated.Tier);
        Assert.Equal(1m, updated.Multiplier);
        var other = await _h.RunScopeAsync(sp => sp.GetRequiredService<IAccountService>().GetProfileAsync(_h.Bob));
        Assert.NotEqual(updated.FullName, other.FullName);
        await Assert.ThrowsAsync<BadRequestException>(() => _h.RunScopeAsync(sp => sp.GetRequiredService<IAccountService>().UpdateProfileAsync(_h.Alice,
            new UpdateProfileRequest("Alice", null, "javascript:alert(1)"))));
    }

    [Fact]
    public async Task Expired_voucher_is_locked_and_errors_explain_expiry()
    {
        var voucher = await _h.AddVoucherAsync("EXPIRED", 5, 1, 10000);
        await _h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            await db.Vouchers.Where(v => v.Id == voucher.Id).ExecuteUpdateAsync(u => u.SetProperty(v => v.ValidUntil, _h.FutureDate.AddDays(-1)));
        });
        var all = await _h.RunScopeAsync(sp => sp.GetRequiredService<IVoucherService>().GetAllAsync(false));
        Assert.False(all.Single(v => v.Id == voucher.Id).IsActive);
        var active = await _h.RunScopeAsync(sp => sp.GetRequiredService<IVoucherService>().GetAllAsync());
        Assert.DoesNotContain(active, v => v.Id == voucher.Id);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _h.RunScopeAsync(sp => sp.GetRequiredService<IVoucherService>().PreviewDiscountAsync("EXPIRED", _h.Alice, 100000)));
        Assert.Contains("hết hạn sử dụng", ex.Message);
        await Assert.ThrowsAsync<BadRequestException>(() => _h.BookAsync(_h.Alice, _h.Court1, _h.SlotNoon, _h.FutureDate.ToString("yyyy-MM-dd"), voucherCode: "EXPIRED"));
    }

    [Fact]
    public async Task Pending_slot_can_be_joined_and_is_labelled_booked()
    {
        await _h.BookAsync(_h.Alice, _h.Court1, _h.SlotNoon, _h.FutureDate.ToString("yyyy-MM-dd"));
        var entry = await _h.RunScopeAsync(sp => sp.GetRequiredService<IWaitlistService>().JoinAsync(_h.Bob, _h.Court1, _h.SlotNoon, _h.FutureDate, 2, null));
        Assert.Equal(WaitlistStatus.Waiting, entry.Status);
        var grid = await _h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().GetAvailabilityAsync(_h.BranchId, _h.FutureDate));
        Assert.Equal("Đã đặt", grid.Single(c => c.CourtId == _h.Court1).Slots.Single(s => s.SlotId == _h.SlotNoon).LockReason);
    }

    private Task Set(CourtDateStatus status, TimeSpan? start = null, TimeSpan? end = null, int? actorId = null) => _h.RunScopeAsync(sp =>
        sp.GetRequiredService<IMaintenanceService>().SetAsync(_h.Court1, _h.FutureDate, status, actorId ?? _h.Manager, "Test maintenance", start, end));

    [Fact]
    public async Task Migration_upgrades_existing_whole_day_closure_without_losing_it()
    {
        var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_h.ConnectionString) { InitialCatalog = _h.DbName + "_Upgrade" };
        await using var db = new BadmintonBookingDbContext(new DbContextOptionsBuilder<BadmintonBookingDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261018074522_AddNotifyWaitlistQrMembership");
            var branch = new Contract.Repositories.Entity.Branch { Name = "Migration test", Address = "Test", OpenTime = new TimeOnly(6, 0), CloseTime = new TimeOnly(23, 0) };
            var court = new Contract.Repositories.Entity.Court { Branch = branch, Code = "TEST", Name = "Test court" };
            db.Courts.Add(court);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CourtDateStatus (CourtId, Date, Status, Note, CreatedAt) VALUES ({court.Id}, {_h.FutureDate}, {"Closed"}, {"Keep existing schedule"}, {DateTime.UtcNow})");
            await db.GetService<IMigrator>().MigrateAsync();
            var row = await db.CourtDateStatuses.SingleAsync();
            Assert.Equal("Keep existing schedule", row.Note);
            Assert.Null(row.StartTime);
            Assert.Null(row.EndTime);
            Assert.True(row.Blocks(new TimeOnly(12, 0), new TimeOnly(13, 0)));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
