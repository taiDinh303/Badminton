using Contract.Repositories.Entity;
using Contract.Services.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Badminton.Core.Enums;
using ModelViews.Booking;
using ModelViews.Payment;
using Xunit;

namespace Badminton.Tests
{
    public class WebhookReplayTests : IAsyncLifetime
    {
        private readonly TestHost _h = new();

        public Task InitializeAsync() => Task.CompletedTask;
        public async Task DisposeAsync() => await _h.DisposeAsync();

        private static string SuccessBody => """{"status":"success"}""";
        private static string FailureBody => """{"status":"failure"}""";

        private async Task<(int BookingId, string GatewayRef)> MakePendingBookingWithGatewayRefAsync()
        {
            var view = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Booking.IBookingService>().CreateAsync(
                    new CreateBookingRequest(_h.Court1, _h.FutureDate.ToString("yyyy-MM-dd"),
                        _h.SlotPeak, 1, null, null, Guid.NewGuid().ToString("N")), _h.Alice));

            // Init a VnPay payment → returns the gateway_ref we'll replay.
            var init = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().InitAsync(
                    new InitPaymentRequest(view!.Id, "VnPay", null), _h.Alice));
            Assert.False(string.IsNullOrEmpty(init!.GatewayRef));

            return (view!.Id, init.GatewayRef!);
        }

        [Fact(DisplayName = "Replaying the same webhook success twice: the confirm transition runs once (DEC-01: no points at confirm)")]
        public async Task ReplayedWebhook_IsIdempotent()
        {
            var (bookingId, refCode) = await MakePendingBookingWithGatewayRefAsync();

            var payload = new PaymentWebhookRequest(refCode, "success", SuccessBody, null);

            var first = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().HandleWebhookAsync("VnPay", payload));
            Assert.True(first!.Accepted);
            Assert.False(first.IdempotentDuplicate);

            var second = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().HandleWebhookAsync("VnPay", payload));
            Assert.True(second!.Accepted);
            Assert.True(second.IdempotentDuplicate);

            var earnRows = await _h.CountPointRowsAsync(bookingId);
            Assert.Equal(0, earnRows);

            // The booking is Confirmed.
            var booking = await _h.RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<Repositories.Context.BadmintonBookingDbContext>();
                return db.Bookings.FirstAsync(b => b.Id == bookingId);
            })!;
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
        }

        [Fact(DisplayName = "Concurrent replays of the same webhook confirm the booking exactly once")]
        public async Task ConcurrentWebhookReplays_NeverDoubleCredit()
        {
            var (bookingId, refCode) = await MakePendingBookingWithGatewayRefAsync();

            int n = 6;
            var results = await Task.WhenAll(Enumerable.Range(0, n).Select(_ =>
                _h.RunScopeAsync(sp =>
                    sp.GetRequiredService<IPaymentService>()
                     .HandleWebhookAsync("VnPay",
                         new PaymentWebhookRequest(refCode, "success", SuccessBody, null)))));

            var accepted = results.Count(r => r!.Accepted);
            var applied = results.Count(r => r!.Accepted && !r.IdempotentDuplicate);

            Assert.True(accepted >= 1, "at least one webhook must be accepted");
            Assert.True(applied == 1, "exactly one webhook can flip the booking");
            Assert.Equal(0, await _h.CountPointRowsAsync(bookingId));
            var booking = await _h.RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<Repositories.Context.BadmintonBookingDbContext>();
                return db.Bookings.FirstAsync(b => b.Id == bookingId);
            })!;
            Assert.Equal(BookingStatus.Confirmed, booking.Status);
        }

        [Fact(DisplayName = "A gateway failure webhook never confirms the booking nor credits points")]
        public async Task FailureWebhook_DoesNotConfirm()
        {
            var (bookingId, refCode) = await MakePendingBookingWithGatewayRefAsync();

            var res = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().HandleWebhookAsync(
                    "VnPay", new PaymentWebhookRequest(refCode, "failure", FailureBody, null)));
            Assert.False(res!.Accepted);

            var booking = await _h.RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<Repositories.Context.BadmintonBookingDbContext>();
                return db.Bookings.FirstAsync(b => b.Id == bookingId);
            })!;
            Assert.Equal(BookingStatus.Pending, booking.Status);
            Assert.Equal(0, await _h.CountPointRowsAsync(bookingId));
        }

        [Fact(DisplayName = "An unknown gateway_ref is rejected, not accepted")]
        public async Task UnknownGatewayRef_IsRejected()
        {
            var res = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().HandleWebhookAsync(
                    "VnPay", new PaymentWebhookRequest("NOPE-NOT-EXISTENT", "success", SuccessBody, null)));
            Assert.False(res!.Accepted);
        }

        [Fact(DisplayName = "P0-3: a webhook with no raw provider body is rejected (no free confirm)")]
        public async Task BlankRawBody_IsRejected()
        {
            var (bookingId, refCode) = await MakePendingBookingWithGatewayRefAsync();

            var res = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().HandleWebhookAsync(
                    "VnPay", new PaymentWebhookRequest(refCode, "success", null, null)));
            Assert.False(res!.Accepted);

            var booking = await _h.RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<Repositories.Context.BadmintonBookingDbContext>();
                return db.Bookings.FirstAsync(b => b.Id == bookingId);
            })!;
            Assert.Equal(BookingStatus.Pending, booking.Status);
            Assert.Equal(0, await _h.CountPointRowsAsync(bookingId));
        }
    }
}
