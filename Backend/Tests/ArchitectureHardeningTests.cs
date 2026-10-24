using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Services.Booking;
using Contract.Services.Points;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelViews.Booking;
using Repositories.Context;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class ArchitectureHardeningTests
{
    [Fact]
    public async Task Duplicate_voucher_release_cannot_consume_another_users_reservation()
    {
        await using var h = new TestHost();
        await h.AddVoucherAsync("RELEASE", 10, 1, 10000);
        var first = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"), voucherCode: "RELEASE");
        await h.BookAsync(h.Bob, h.Court2, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"), voucherCode: "RELEASE");
        await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => h.RunScopeAsync(sp =>
            sp.GetRequiredService<Contract.Services.Voucher.IVoucherService>().ReleaseVoucherAsync(first.VoucherRedemptionId!.Value))));
        Assert.Equal(1, (await h.GetVoucherAsync("RELEASE")).UsedQty);
    }

    [Fact]
    public async Task Concurrent_no_shows_count_each_booking_for_the_same_customer()
    {
        await using var h = new TestHost();
        var first = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotMorning);
        var second = await h.MakeConfirmedBookingAsync(h.Alice, h.Court2, h.SlotMorning);
        var clock = Badminton.Core.Utils.HanoiTime.NowUtc;
        try
        {
            Badminton.Core.Utils.HanoiTime.NowUtc = () => h.HanoiNowUtc.AddHours(2);
            await Task.WhenAll(new[] { first.Id, second.Id }.Select(id => h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>().MarkNoShowAsync(id, h.Manager))));
            Assert.Equal(2, (await h.GetUserAsync(h.Alice)).NoShowCount);
        }
        finally { Badminton.Core.Utils.HanoiTime.NowUtc = clock; }
    }

    [Fact]
    public async Task Redeem_replay_returns_the_persisted_discount_without_debiting_again()
    {
        await using var h = new TestHost();
        await h.CountPointsIntoAsync(h.Alice, 5000, "replay balance");
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"), 5000);
        var discount = await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>()
            .RedeemAsync(h.Alice, booking.Id, 1000, booking.BookingCode));
        Assert.Equal(100000, discount);
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Alice)));
    }

    [Fact]
    public async Task Fingerprint_migration_preserves_legacy_bookings_and_does_not_invent_their_payload()
    {
        await using var h = new TestHost(useMigrations: true);
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var migrations = db.Database.GetMigrations().ToArray();
            var fingerprintIndex = Array.FindIndex(migrations, m => m.EndsWith("_AddBookingRequestFingerprint", StringComparison.Ordinal));
            Assert.True(fingerprintIndex > 0, "Fingerprint migration must have a predecessor.");
            await db.GetService<IMigrator>().MigrateAsync(migrations[fingerprintIndex - 1]);
            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();
            var legacy = await db.Bookings.SingleAsync(b => b.Id == booking.Id);
            Assert.Equal(booking.BookingCode, legacy.BookingCode);
            Assert.Equal(booking.PayableAmount, legacy.PayableAmount);
            Assert.Null(legacy.RequestFingerprint);
        });
    }

    [Fact]
    public async Task Automatic_completion_uses_membership_and_is_idempotent()
    {
        await using var h = new TestHost();
        await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var user = await db.Users.FindAsync(h.Alice);
            user!.Tier = MembershipTier.Gold;
            user.MembershipExpiresAt = DateTime.UtcNow.AddDays(30);
            await db.SaveChangesAsync();
        });
        var booking = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        Assert.Null(await h.RunScopeAsync(sp => sp.GetRequiredService<BookingCompletionService>()
            .CompleteAsync(booking.Id, onlyIfDue: true)));
        var originalClock = Badminton.Core.Utils.HanoiTime.NowUtc;
        try
        {
            Badminton.Core.Utils.HanoiTime.NowUtc = () => h.HanoiNowUtc.AddHours(5);
            var completed = await h.RunScopeAsync(sp => sp.GetRequiredService<BookingCompletionService>()
                .CompleteAsync(booking.Id, onlyIfDue: true));
            Assert.Equal(booking.PayableAmount * 2, completed!.PointsEarned);
            Assert.Null(await h.RunScopeAsync(sp => sp.GetRequiredService<BookingCompletionService>()
                .CompleteAsync(booking.Id, onlyIfDue: true)));
            Assert.Equal(1, await h.CountPointRowsAsync(booking.Id));
        }
        finally { Badminton.Core.Utils.HanoiTime.NowUtc = originalClock; }
    }

    [Fact]
    public async Task Expiry_restores_points_once_and_never_cancels_a_paid_order()
    {
        await using var h = new TestHost();
        await h.CountPointsIntoAsync(h.Alice, 5000, "expiry balance");
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"), 5000);
        var now = DateTime.UtcNow.AddMinutes(11);
        Assert.True(await h.RunScopeAsync(sp => sp.GetRequiredService<BookingExpiryService>().ExpireAsync(booking.Id, now)));
        Assert.False(await h.RunScopeAsync(sp => sp.GetRequiredService<BookingExpiryService>().ExpireAsync(booking.Id, now)));
        Assert.Equal(5000, await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Alice)));
        var paid = await h.MakeConfirmedBookingAsync(h.Bob, h.Court2, h.SlotNoon);
        Assert.False(await h.RunScopeAsync(sp => sp.GetRequiredService<BookingExpiryService>().ExpireAsync(paid.Id, now)));
        Assert.Equal(BookingStatus.Confirmed, (await h.GetBookingAsync(paid.Id)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rejected_refund_preserves_original_state_and_points(bool completed)
    {
        await using var h = new TestHost(configure: services =>
        {
            services.RemoveAll<Badminton.Core.Domain.IPaymentGateway>();
            services.AddScoped<Badminton.Core.Domain.IPaymentGateway, RejectingCashGateway>();
        });
        var booking = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        if (completed)
            await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().MarkCompletedAsync(booking.Id, h.Manager));
        var balance = await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Alice));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp =>
            sp.GetRequiredService<Contract.Services.Payment.IPaymentService>().RefundForMaintenanceAsync(booking.Id, h.Manager)));
        Assert.Equal(completed ? BookingStatus.Completed : BookingStatus.Confirmed, (await h.GetBookingAsync(booking.Id)).Status);
        Assert.Equal(balance, await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Alice)));
        Assert.Equal(0, await h.CountRefundRowsAsync(booking.Id));
    }

    private sealed class RejectingCashGateway : Badminton.Core.Domain.IPaymentGateway
    {
        public PaymentMethod Method => PaymentMethod.Cash;
        public Task<Badminton.Core.Domain.PaymentInitResult> InitAsync(string id, decimal amount, string url, CancellationToken ct = default) =>
            Task.FromResult(Badminton.Core.Domain.PaymentInitResult.Fail("onsite"));
        public Task<Badminton.Core.Domain.WebhookOutcome> VerifyWebhookAsync(Badminton.Core.Domain.RawWebhook raw, CancellationToken ct = default) =>
            Task.FromResult(new Badminton.Core.Domain.WebhookOutcome(false, false, null, "onsite"));
        public Task<Badminton.Core.Domain.RefundResult> RefundAsync(string id, decimal amount, string reason, CancellationToken ct = default) =>
            Task.FromResult(Badminton.Core.Domain.RefundResult.Fail("synthetic rejection"));
    }

    [Theory]
    [InlineData("voucher")]
    [InlineData("points")]
    [InlineData("note")]
    [InlineData("actor")]
    public async Task Retry_key_rejects_a_different_full_payload(string field)
    {
        await using var h = new TestHost();
        var request = new CreateBookingRequest(h.Court1, h.FutureDate.ToString("yyyy-MM-dd"),
            h.SlotNoon, IdempotencyKey: Guid.NewGuid().ToString("N"));
        var first = await Create(h, request, h.Alice);
        Assert.Equal(first.Id, (await Create(h, request, h.Alice)).Id);
        var changed = field switch
        {
            "voucher" => request with { VoucherCode = "DIFFERENT" },
            "points" => request with { PointsToRedeem = 1000 },
            "note" => request with { Note = "new note" },
            _ => request
        };
        await Assert.ThrowsAsync<ConflictException>(() => Create(h, changed, field == "actor" ? h.Bob : h.Alice));
    }

    [Fact]
    public async Task Concurrent_identical_requests_return_one_booking()
    {
        await using var h = new TestHost();
        var request = new CreateBookingRequest(h.Court1, h.FutureDate.ToString("yyyy-MM-dd"),
            h.SlotNoon, IdempotencyKey: Guid.NewGuid().ToString("N"));
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Create(h, request, h.Alice)));
        Assert.Single(results.Select(b => b.Id).Distinct());
    }

    [Fact]
    public async Task Concurrent_redemptions_on_different_courts_cannot_overspend()
    {
        await using var h = new TestHost();
        await h.CountPointsIntoAsync(h.Alice, 5000, "balance fixture");
        async Task<bool> Book(int court)
        {
            try
            {
                await h.BookAsync(h.Alice, court, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"), 5000);
                return true;
            }
            catch (ConflictException) { return false; }
        }
        var results = await Task.WhenAll(Book(h.Court1), Book(h.Court2));
        Assert.Single(results, success => success);
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Alice)));
    }

    [Fact]
    public async Task Failure_after_ledger_save_rolls_back_completion_and_points()
    {
        var failure = new FailAfterLedgerSave();
        await using var h = new TestHost(configure: services =>
            services.AddSingleton<IInterceptor>(failure));
        var booking = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        failure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.RunScopeAsync(sp =>
            sp.GetRequiredService<IBookingService>().MarkCompletedAsync(booking.Id, h.Manager)));
        failure.Enabled = false;
        Assert.Equal(BookingStatus.Confirmed, (await h.GetBookingAsync(booking.Id)).Status);
        Assert.Equal(0, await h.CountPointRowsAsync(booking.Id));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().MarkCompletedAsync(booking.Id, h.Manager));
        Assert.Equal(1, await h.CountPointRowsAsync(booking.Id));
    }

    [Fact]
    public async Task Weekend_availability_price_matches_the_created_order()
    {
        await using var h = new TestHost();
        var date = h.FutureDate;
        while (date.DayOfWeek != DayOfWeek.Saturday) date = date.AddDays(1);
        var courts = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().GetAvailabilityAsync(h.BranchId, date));
        var quote = courts.Single(c => c.CourtId == h.Court1);
        Assert.Equal(120_000, quote.PriceLowPeak);
        var booking = await Create(h, new(h.Court1, date.ToString("yyyy-MM-dd"), h.SlotNoon, PlayerCount: 2), h.Alice);
        Assert.Equal(quote.PriceLowPeak, booking.TotalAmount);
    }

    [Fact]
    public async Task Gold_completion_and_full_refund_use_the_same_point_snapshot()
    {
        await using var h = new TestHost();
        await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var user = await db.Users.FindAsync(h.Alice);
            user!.Tier = MembershipTier.Gold;
            user.MembershipExpiresAt = DateTime.UtcNow.AddDays(30);
            await db.SaveChangesAsync();
        });
        var booking = await h.MakeConfirmedBookingAsync(h.Alice, h.Court1, h.SlotNoon);
        var completed = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>()
            .MarkCompletedAsync(booking.Id, h.Manager));
        Assert.Equal(booking.PayableAmount * 2, completed.PointsEarned);
        await h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Payment.IPaymentService>()
            .RefundForMaintenanceAsync(booking.Id, h.Manager));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<IPointService>().GetBalanceAsync(h.Alice)));
    }

    private static Task<BookingView> Create(TestHost h, CreateBookingRequest request, int actor) =>
        h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CreateAsync(request, actor));

    private sealed class FailAfterLedgerSave : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled && eventData.Context!.ChangeTracker.Entries<PointLedger>().Any())
                throw new InvalidOperationException("Injected failure after ledger persistence.");
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    public void Login_lockout_is_thread_safe_and_expires_after_fifteen_minutes()
    {
        var time = new TestTime();
        var tracker = new LoginAttemptTracker(time);
        Parallel.For(0, 5, _ => tracker.RecordFailure("synthetic-phone"));
        Assert.True(tracker.IsLocked("synthetic-phone"));
        time.Now += TimeSpan.FromMinutes(10);
        Assert.True(tracker.IsLocked("synthetic-phone"));
        time.Now += TimeSpan.FromMinutes(5);
        Assert.False(tracker.IsLocked("synthetic-phone"));
        tracker.RecordFailure("synthetic-phone");
        Assert.False(tracker.IsLocked("synthetic-phone"));
    }

    private sealed class TestTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
