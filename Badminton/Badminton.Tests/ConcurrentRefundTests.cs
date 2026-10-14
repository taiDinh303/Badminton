using Contract.Services.Booking;
using Contract.Services.Payment;
using Microsoft.Extensions.DependencyInjection;
using M.Core.Base;
using M.Core.Enums;
using ModelViews.Booking;
using ModelViews.Payment;
using Xunit;

namespace Badminton.Tests
{
    public class ConcurrentRefundTests : IAsyncLifetime
    {
        private readonly TestHost _h = new();

        public Task InitializeAsync() => Task.CompletedTask;
        public async Task DisposeAsync() => await _h.DisposeAsync();

        private async Task<int> MakeConfirmedBookingAsync(int user, int court, int slot)
        {
            var view = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>().CreateAsync(
                    new CreateBookingRequest(court, _h.FutureDate.ToString("yyyy-MM-dd"), slot,
                        1, null, null, Guid.NewGuid().ToString("N")), user));

            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>()
                 .ConfirmOnSiteAsync(view!.Id, "Cash", _h.Manager));
            return view!.Id;
        }

        [Fact(DisplayName = "Two concurrent refunds of the same booking produce exactly one refund row")]
        public async Task ConcurrentRefunds_ProduceOneRow()
        {
            var bookingId = await MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotPeak);

            // Two managers refund the same confirmed booking at the same
            // instant, each in its own scope.
            var tasks = Enumerable.Range(0, 2).Select(_ =>
                _h.RunScopeAsync(sp =>
                    sp.GetRequiredService<IPaymentService>()
                     .RefundAsync(bookingId, _h.Manager, "test concurrent refund")))
                .ToList();
            var results = await Task.WhenAll(tasks);

            // Both return a refund view; the loser returns the winner's row
            // (same deterministic GatewayRef), so no exception is expected.
            var refundRows = await _h.CountRefundRowsAsync(bookingId);
            Assert.Equal(1, refundRows);

            // Both views refer to the single refund record.
            var refs = results.Select(r => r!.GatewayRef).Distinct().ToList();
            Assert.Single(refs);
            Assert.Equal($"REFUND-{bookingId}", refs[0]);
        }

        [Fact(DisplayName = "Replaying a refund request returns the existing refund (idempotent)")]
        public async Task ReplayedRefund_IsIdempotent()
        {
            var bookingId = await MakeConfirmedBookingAsync(_h.Bob, _h.Court2, _h.SlotNoon);

            var first = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().RefundAsync(bookingId, _h.Manager));
            Assert.NotNull(first);

            var second = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().RefundAsync(bookingId, _h.Manager));
            Assert.NotNull(second);
            Assert.Equal(first!.GatewayRef, second!.GatewayRef);

            Assert.Equal(1, await _h.CountRefundRowsAsync(bookingId));
        }

        [Fact(DisplayName = "Clawback mirrors the refund: points are reversed exactly once")]
        public async Task Refund_ClawsBackPointsOnce()
        {
            var bookingId = await MakeConfirmedBookingAsync(_h.Carol, _h.Court1, _h.SlotNoon);

            // After confirm: 1 earn row.
            var beforeRefund = await _h.CountPointRowsAsync(bookingId);
            Assert.True(beforeRefund == 1, "expected exactly 1 earn row after confirm");

            // A full refund (≥2h out) claws back the whole credit.
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().RefundAsync(bookingId, _h.Manager));
            var afterRefund = await _h.CountPointRowsAsync(bookingId);
            Assert.True(afterRefund == 2, $"expected earn+clawback=2 rows, got {afterRefund}");

            // A second refund must not add another clawback.
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>().RefundAsync(bookingId, _h.Manager));
            Assert.True(await _h.CountPointRowsAsync(bookingId) == 2,
                "replayed refund must not add a second clawback row");
        }
    }
}
