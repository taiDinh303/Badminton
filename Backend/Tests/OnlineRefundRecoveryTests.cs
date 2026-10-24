using Badminton.Core.Base;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Store;
using Contract.Repositories.Entity;
using Contract.Services.Booking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Repositories.Context;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class OnlineRefundRecoveryTests
{
    private static TestHost Host(Provider provider, CrashAfterRefundSave? crash = null) => new(configure: services =>
    {
        services.AddSingleton<IOnlineRefundClient>(provider);
        services.AddSingleton<IOptions<OnlineRefundSettings>>(Options.Create(new OnlineRefundSettings { Enabled = true }));
        services.AddScoped<OnlineRefundService>();
        services.RemoveAll<IPaymentGateway>();
        services.AddSingleton<IPaymentGateway>(new OnSiteGateway(PaymentMethod.Cash));
        services.AddSingleton<IPaymentGateway>(new RealOnlineGateway());
        if (crash is not null) services.AddSingleton<IInterceptor>(crash);
    });

    private static async Task<(int Booking, int Receipt)> Receipt(TestHost h, string? voucher = null)
    {
        if (voucher is not null) await h.AddVoucherAsync(voucher, 10, 1, 10000);
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"), voucherCode: voucher);
        var receipt = await h.ConfirmOnSiteAsync(booking.Id, h.Manager);
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.Where(p => p.Id == receipt.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Method, PaymentMethod.MoMo).SetProperty(p => p.ProviderTransactionId, "123456")
                .SetProperty(p => p.GatewayRef, "PURCHASE-" + receipt.Id)));
        return (booking.Id, receipt.Id);
    }

    [Fact]
    public async Task Uncertain_refund_is_durable_and_restart_queries_without_another_submission()
    {
        var provider = new Provider { Settled = false };
        await using var h = Host(provider);
        var (booking, receipt) = await Receipt(h, "KEEP");
        h.ActAs(h.Alice, Role.Customer);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CancelAsync(booking, h.Alice)));
        Assert.Equal(BookingStatus.Confirmed, (await h.GetBookingAsync(booking)).Status);
        Assert.Equal(1, (await h.GetVoucherAsync("KEEP")).UsedQty);
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Set<OnlineRefundOperation>().CountAsync()));
        provider.Settled = true;
        h.ActAs(h.Manager, Role.Manager);
        await h.RunScopeAsync(sp => sp.GetRequiredService<OnlineRefundService>().RequestAsync(receipt, h.Manager, null, lateReceipt: true));
        Assert.Equal(1, provider.Submissions); Assert.Equal(1, provider.Queries);
        Assert.Equal(BookingStatus.Cancelled, (await h.GetBookingAsync(booking)).Status);
        Assert.Equal(0, (await h.GetVoucherAsync("KEEP")).UsedQty);
        Assert.Equal(1, await h.CountRefundRowsAsync(booking));
        Assert.False(await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.Where(p => p.Id == receipt).Select(p => p.ReconciliationRequired).SingleAsync()));
    }

    [Fact]
    public async Task Crash_after_provider_success_rolls_back_ledger_and_query_finishes_once()
    {
        var provider = new Provider(); var crash = new CrashAfterRefundSave();
        await using var h = Host(provider, crash);
        var (booking, receipt) = await Receipt(h);
        crash.Enabled = true;
        await Assert.ThrowsAsync<IOException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<OnlineRefundService>().RequestAsync(receipt, h.Manager, "maintenance", maintenance: true)));
        Assert.Equal(BookingStatus.Confirmed, (await h.GetBookingAsync(booking)).Status);
        Assert.Equal(0, await h.CountRefundRowsAsync(booking));
        crash.Enabled = false;
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => h.RunScopeAsync(sp => sp.GetRequiredService<OnlineRefundService>().RequestAsync(receipt, h.Manager, null, lateReceipt: true))));
        Assert.Equal(1, provider.Submissions);
        Assert.Equal(1, await h.CountRefundRowsAsync(booking));
        Assert.Equal(BookingStatus.Refunded, (await h.GetBookingAsync(booking)).Status);
    }

    [Fact]
    public async Task Late_receipt_refund_keeps_cancelled_booking_and_cannot_be_requested_across_branches()
    {
        var provider = new Provider(); await using var h = Host(provider);
        var (booking, receipt) = await Receipt(h);
        await h.RunScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            await db.Bookings.Where(b => b.Id == booking).ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Cancelled));
            await db.Payments.Where(p => p.Id == receipt).ExecuteUpdateAsync(s => s.SetProperty(p => p.ReconciliationRequired, true).SetProperty(p => p.ReconciliationReason, "late_receipt"));
        });
        h.ActAs(h.ManagerOtherBranch, Role.Manager);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<OnlineRefundService>().RequestAsync(receipt, h.ManagerOtherBranch, null, lateReceipt: true)));
        Assert.Equal(0, provider.Submissions);
        h.ActAs(h.Manager, Role.Manager);
        await h.RunScopeAsync(sp => sp.GetRequiredService<OnlineRefundService>().RequestAsync(receipt, h.Manager, null, lateReceipt: true));
        Assert.Equal(BookingStatus.Cancelled, (await h.GetBookingAsync(booking)).Status);
        Assert.Equal(1, await h.CountRefundRowsAsync(booking));
    }

    [Fact]
    public async Task Missing_verified_provider_id_never_attempts_a_refund()
    {
        var provider = new Provider(); await using var h = Host(provider);
        var (_, receipt) = await Receipt(h);
        await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.Where(p => p.Id == receipt)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ProviderTransactionId, (string?)null)));
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<OnlineRefundService>().RequestAsync(receipt, h.Manager, null)));
        Assert.Equal(0, provider.Submissions);
    }

    private sealed class Provider : IOnlineRefundClient
    {
        public bool Settled = true;
        public int Submissions, Queries;
        public string CreateRequestId(PaymentMethod method) => Guid.NewGuid().ToString("N");
        public Task<OnlineRefundOutcome> ExecuteAsync(OnlineRefundRequest request, bool queryOnly, CancellationToken ct = default)
        {
            if (queryOnly) Interlocked.Increment(ref Queries); else Interlocked.Increment(ref Submissions);
            return Task.FromResult(new OnlineRefundOutcome(Settled, "synthetic"));
        }
    }
    private sealed class RealOnlineGateway : IPaymentGateway
    {
        public PaymentMethod Method => PaymentMethod.MoMo;
        public Task<PaymentInitResult> InitAsync(string id, decimal amount, string url, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<WebhookOutcome> VerifyWebhookAsync(RawWebhook raw, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<RefundResult> RefundAsync(string id, decimal amount, string reason, CancellationToken ct = default) => throw new InvalidOperationException("Legacy refund transport must never be called.");
    }
    private sealed class CrashAfterRefundSave : SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        {
            if (Enabled && data.Context!.ChangeTracker.Entries<Payment>().Any(e => e.Entity.GatewayRef?.StartsWith("ONLINE-REFUND-") == true))
                throw new IOException("Synthetic crash after refund receipt persistence.");
            return ValueTask.FromResult(result);
        }
    }
}
