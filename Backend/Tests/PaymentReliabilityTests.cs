using Badminton.Core.Base;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Services.Payment;
using Contract.Services.Booking;
using Contract.Services.Notify;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelViews.Payment;
using Repositories.Context;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class PaymentReliabilityTests
{
    private static TestHost Host(Gateway gateway) => new(configure: services => {
        services.RemoveAll<IPaymentGateway>(); services.AddSingleton<IPaymentGateway>(gateway);
        services.AddSingleton<IPaymentGateway>(new OnSiteGateway(PaymentMethod.Cash));
    });

    [Fact]
    public async Task Init_is_durable_before_io_and_replays_reuse_one_checkout_without_another_charge()
    {
        var gateway = new Gateway(); await using var h = Host(gateway);
        var b = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        h.ActAs(h.Alice, Role.Customer);
        gateway.Initialize = async (reference, amount) => {
            var row = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.SingleAsync());
            Assert.Equal(reference, row.GatewayRef); Assert.Equal(h.Alice, row.UserId); Assert.Equal(amount, row.Amount);
            Assert.True(row.ReconciliationRequired);
            return PaymentInitResult.Success(reference, "https://checkout.example.test/pay");
        };
        var request = new InitPaymentRequest(b.Id, "MoMo", "https://web.example.test/return");
        var first = await h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().InitAsync(request, h.Alice));
        var retries = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().InitAsync(request, h.Alice))));
        Assert.All(retries, r => Assert.Equal(first, r)); Assert.Equal(1, gateway.Calls);
        Assert.Matches("^[0-9]{6}_[a-f0-9]{32}$", first.GatewayRef!);
        Assert.False(await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.Select(p => p.ReconciliationRequired).SingleAsync()));
    }

    [Fact]
    public async Task Timeout_remains_visible_and_is_not_retried_after_a_new_scope()
    {
        var gateway = new Gateway { Initialize = (_, _) => throw new TimeoutException("synthetic-secret") }; await using var h = Host(gateway);
        var b = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        h.ActAs(h.Alice, Role.Customer);
        var request = new InitPaymentRequest(b.Id, "MoMo", "https://web.example.test/return");
        var error = await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().InitAsync(request, h.Alice)));
        Assert.DoesNotContain("synthetic-secret", error.Message);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().InitAsync(request, h.Alice)));
        Assert.Equal(1, gateway.Calls);
        h.ActAs(h.Manager, Role.Manager);
        var review = await h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().GetReconciliationAsync(null, new(1, 50)));
        Assert.Single(review.Items); Assert.Equal("initialization_uncertain", review.Items[0].Reason);
        h.ActAs(h.ManagerOtherBranch, Role.Manager);
        Assert.Empty((await h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().GetReconciliationAsync(null, new(1, 50)))).Items);
        await Assert.ThrowsAsync<ConflictException>(() => h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().GetReconciliationAsync(h.BranchId, new(1, 50))));
    }

    [Fact]
    public async Task Callback_arriving_during_init_cannot_be_lost_or_downgraded_to_pending()
    {
        var gateway = new Gateway(); await using var h = Host(gateway);
        var b = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        h.ActAs(h.Alice, Role.Customer);
        gateway.Initialize = async (reference, _) => {
            var result = await h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().HandleWebhookAsync("MoMo", new(reference, "", "signed-fixture")));
            Assert.True(result.Accepted);
            return PaymentInitResult.Success(reference, "https://checkout.example.test/pay");
        };
        await h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().InitAsync(new(b.Id, "MoMo", "https://web.example.test/return"), h.Alice));
        Assert.Equal(BookingStatus.Confirmed, (await h.GetBookingAsync(b.Id)).Status);
        var payment = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.SingleAsync());
        Assert.Equal(PaymentStatus.Success, payment.Status); Assert.False(payment.ReconciliationRequired); Assert.NotNull(payment.GatewayUrl);
    }

    [Fact]
    public async Task Late_receipt_keeps_cancelled_booking_and_is_flagged_once_for_staff()
    {
        var gateway = new Gateway(); await using var h = Host(gateway);
        var b = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        h.ActAs(h.Alice, Role.Customer);
        var init = await h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().InitAsync(new(b.Id, "MoMo", "https://web.example.test/return"), h.Alice));
        await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CancelAsync(b.Id, h.Alice));
        var first = await h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().HandleWebhookAsync("MoMo", new(init.GatewayRef!, "", "signed-fixture")));
        Assert.True(first.Accepted); Assert.False(first.IdempotentDuplicate);
        Assert.True((await h.RunScopeAsync(sp => sp.GetRequiredService<IPaymentService>().HandleWebhookAsync("MoMo", new(init.GatewayRef!, "", "signed-fixture")))).IdempotentDuplicate);
        Assert.Equal(BookingStatus.Cancelled, (await h.GetBookingAsync(b.Id)).Status);
        var row = await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.SingleAsync());
        Assert.Equal(PaymentStatus.Success, row.Status); Assert.Equal("late_receipt", row.ReconciliationReason); Assert.True(row.ReconciliationRequired);
    }

    [Fact]
    public async Task Notification_outbox_survives_failure_after_booking_commit_and_delivers_after_restart()
    {
        var channel = new CountingChannel();
        await using var h = new TestHost(configure: services => {
            services.RemoveAll<INotifyChannel>(); services.AddSingleton<INotifyChannel>(channel);
            services.RemoveAll<INotifyService>(); services.AddScoped<INotifyService>(sp => new CrashNotify(ActivatorUtilities.CreateInstance<NotifyService>(sp)));
        });
        var booking = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        await Assert.ThrowsAsync<IOException>(() => h.ConfirmOnSiteAsync(booking.Id, h.Manager));
        Assert.Equal(BookingStatus.Confirmed, (await h.GetBookingAsync(booking.Id)).Status);
        Assert.Equal(0, channel.Calls);
        await h.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var row = await db.Notifications.SingleAsync(n => n.BookingId == booking.Id);
            Assert.Equal(NotifyStatus.Pending, row.Status);
            row.UpdatedAt = DateTime.UtcNow.AddMinutes(-3); await db.SaveChangesAsync();
        });
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<INotifyService>().RetryPendingAsync()));
        Assert.Equal(0, await h.RunScopeAsync(sp => sp.GetRequiredService<INotifyService>().RetryPendingAsync()));
        Assert.Equal(1, channel.Calls);
    }

    [Fact]
    public async Task Waitlist_recovers_a_booking_committed_before_its_promotion_record()
    {
        await using var h = new TestHost();
        var original = await h.BookAsync(h.Alice, h.Court1, h.SlotNoon, h.FutureDate.ToString("yyyy-MM-dd"));
        var waiter = await h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Waitlist.IWaitlistService>().JoinAsync(h.Bob, h.Court1, h.SlotNoon, h.FutureDate, 1, null));
        h.ActAs(h.Alice, Role.Customer);
        await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CancelAsync(original.Id, h.Alice));
        var created = await h.RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().CreateAsync(new(h.Court1,
            h.FutureDate.ToString("yyyy-MM-dd"), h.SlotNoon, 1, IdempotencyKey: $"waitlist:{waiter.Id}"), h.Bob));
        var recovered = await h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Waitlist.IWaitlistService>().PromoteAsync(h.Court1, h.SlotNoon, h.FutureDate));
        Assert.Equal(created.Id, recovered!.Id);
        Assert.Equal(WaitlistStatus.Promoted, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Set<WaitlistEntry>().Where(w => w.Id == waiter.Id).Select(w => w.Status).SingleAsync()));
        Assert.Equal(1, await h.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Notifications.CountAsync(n => n.BookingId == created.Id && n.Event == NotifyEvent.WaitlistPromoted)));
    }

    private sealed class CountingChannel : INotifyChannel
    {
        public string Name => "log"; public bool IsConfigured => true; public int Calls;
        public Task SendAsync(User user, NotifyEvent evt, string body) { Calls++; return Task.CompletedTask; }
    }
    private sealed class CrashNotify(NotifyService inner) : INotifyService
    {
        public Task<IReadOnlyList<Notification>> EnqueueAndSendAsync(NotifyEvent evt, int userId, int? bookingId, string body) => inner.EnqueueAndSendAsync(evt, userId, bookingId, body);
        public Task QueueBookingEventAsync(NotifyEvent evt, int bookingId) => inner.QueueBookingEventAsync(evt, bookingId);
        public Task QueueWaitlistPromotedAsync(int id, int bookingId) => inner.QueueWaitlistPromotedAsync(id, bookingId);
        public Task<int> RetryPendingAsync() => inner.RetryPendingAsync();
        public Task NotifyBookingConfirmedAsync(int bookingId) => throw new IOException("Synthetic crash after database commit.");
        public Task NotifyBookingExpiredAsync(int bookingId) => inner.NotifyBookingExpiredAsync(bookingId);
        public Task NotifyNoShowAsync(int bookingId) => inner.NotifyNoShowAsync(bookingId);
        public Task NotifyWaitlistPromotedAsync(int id, int bookingId) => inner.NotifyWaitlistPromotedAsync(id, bookingId);
    }

    private sealed class Gateway : IPaymentGateway
    {
        public PaymentMethod Method => PaymentMethod.MoMo;
        public int Calls; private int _amount;
        public Func<string, int, Task<PaymentInitResult>> Initialize = (r, _) => Task.FromResult(PaymentInitResult.Success(r, "https://checkout.example.test/pay"));
        public Task<PaymentInitResult> InitAsync(string bookingId, decimal amount, string returnUrl, CancellationToken ct = default) { Calls++; _amount = (int)amount; return Initialize(bookingId, _amount); }
        public Task<WebhookOutcome> VerifyWebhookAsync(RawWebhook raw, CancellationToken ct = default) => Task.FromResult(new WebhookOutcome(true, true, raw.GatewayRef, null, _amount, "VND"));
        public Task<RefundResult> RefundAsync(string bookingId, decimal amount, string reason, CancellationToken ct = default) => throw new InvalidOperationException("No refund network in these tests.");
    }
}
