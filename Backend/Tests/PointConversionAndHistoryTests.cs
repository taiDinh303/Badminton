using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Services.Points;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Context;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class PointConversionAndHistoryTests
{
    [Fact]
    public async Task Migration_converts_once_without_rewriting_ledger_and_restores_legacy_points_in_new_units()
    {
        await using var h = new TestHost(useMigrations: true);
        await h.CountPointsIntoAsync(h.Alice, 150999, "legacy balance");
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"), 1000);
        var original = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().PointLedger.AsNoTracking()
            .Where(p => p.UserId == h.Alice).OrderBy(p => p.Id).Select(p => new { p.Id, p.Delta, p.LedgerKey }).ToListAsync());
        await h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var migrations = db.Database.GetMigrations().ToList();
            var latest = migrations.FindIndex(x => x.EndsWith("_HardenNonPaymentWorkflows"));
            Assert.True(latest > 0);
            await db.GetService<IMigrator>().MigrateAsync(migrations[latest - 1]);
            await db.GetService<IMigrator>().MigrateAsync();
            await db.GetService<IMigrator>().MigrateAsync();
        });
        var balance = await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Alice));
        Assert.Equal(149, balance);
        await h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            foreach (var old in original) {
                var row = await db.PointLedger.SingleAsync(p => p.Id == old.Id);
                Assert.Equal(old.Delta, row.Delta); Assert.Equal(old.LedgerKey, row.LedgerKey);
            }
            Assert.Equal(1, await db.PointLedger.CountAsync(p => p.LedgerKey == $"unit-v2:{h.Alice}"));
        });
        await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().RestoreAsync(h.Alice, booking.Id, 1000, booking.BookingCode));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().RestoreAsync(h.Alice, booking.Id, 1000, booking.BookingCode));
        Assert.Equal(150, await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Alice)));
    }

    [Fact]
    public async Task Recorded_occupancy_capacity_survives_later_court_deactivation_and_labels_missing_history()
    {
        await using var h = new TestHost();
        await h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Report.IInventoryHistoryService>().CaptureTodayAsync());
        h.ActAs(h.Manager, Role.Manager);
        var before = await h.RunScopeAsync(sp => ActivatorUtilities.CreateInstance<ReportService>(sp).GetOccupancyAsync(h.FutureDate, h.FutureDate));
        HanoiTime.NowUtc = () => h.HanoiNowUtc.AddDays(1);
        await h.RunScopeAsync(sp => ActivatorUtilities.CreateInstance<CourtService>(sp).DeleteAsync(h.Court1, h.Manager));
        var historical = await h.RunScopeAsync(sp => ActivatorUtilities.CreateInstance<ReportService>(sp).GetOccupancyAsync(h.FutureDate, h.FutureDate));
        Assert.Equal(before.Single().TotalSlots, historical.Single().TotalSlots);
        Assert.False(historical.Single().HistoricalEstimate);
        var unknown = await h.RunScopeAsync(sp => ActivatorUtilities.CreateInstance<ReportService>(sp).GetOccupancyAsync(h.FutureDate.AddDays(-1), h.FutureDate.AddDays(-1)));
        Assert.True(unknown.Single().HistoricalEstimate);
    }
}
