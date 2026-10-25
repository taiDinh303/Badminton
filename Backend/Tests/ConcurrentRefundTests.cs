using Contract.Services.Booking;
using Contract.Services.Payment;
using Microsoft.Extensions.DependencyInjection;
using Badminton.Core.Base;
using Badminton.Core.Enums;
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

            var tasks = Enumerable.Range(0, 2).Select(_ =>
                _h.RunScopeAsync(sp =>
                    sp.GetRequiredService<IPaymentService>()
                     .RefundAsync(bookingId, _h.Manager, "test concurrent refund")))
                .ToList();
            var results = await Task.WhenAll(tasks);

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

        [Fact(DisplayName = "DEC-01: a confirmed-but-never-completed booking banks no points, so a full refund claws back nothing")]
        public async Task Refund_UncompletedBooking_HasNoPointsToClawback()
        {
            var bookingId = await MakeConfirmedBookingAsync(_h.Carol, _h.Court1, _h.SlotNoon);

            // DEC-01: points bank at Completed, not Confirmed → zero rows now.
            Assert.Equal(0, await _h.CountPointRowsAsync(bookingId));

            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>()
                 .RefundAsync(bookingId, _h.Manager, "full refund"));
            Assert.Equal(0, await _h.CountPointRowsAsync(bookingId));
        }

        [Fact(DisplayName = "DEC-02: a completed booking's points are clawed back exactly once on a full refund")]
        public async Task Refund_CompletedBooking_ClawsBackPointsOnce()
        {
            var bookingId = await MakeConfirmedBookingAsync(_h.Carol, _h.Court1, _h.SlotNoon);

            // Complete it first (DEC-01 banks the earn row now).
            _h.ActAs(_h.Manager, Role.Manager);
            await _h.CompleteAsPlayedAsync(bookingId, _h.Manager, "da choi");
            Assert.Equal(1, await _h.CountPointRowsAsync(bookingId));

            // A full refund (≥2h out) claws back the whole credit — once.
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>()
                 .RefundAsync(bookingId, _h.Manager, "full refund test"));
            Assert.Equal(2, await _h.CountPointRowsAsync(bookingId)); // earn + clawback

            // A replayed refund must not add another clawback.
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>()
                 .RefundAsync(bookingId, _h.Manager, "replay"));
            var finalRows = await _h.CountPointRowsAsync(bookingId);
            Assert.True(finalRows == 2,
                $"replayed refund must not add a second clawback row (got {finalRows})");
        }
    }
}
