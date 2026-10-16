using Contract.Services.Booking;
using Microsoft.Extensions.DependencyInjection;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Xunit;

namespace Badminton.Tests
{
    public class TimezoneGuardTests : IAsyncLifetime
    {
        private readonly TestHost _h = new();

        public Task InitializeAsync() => Task.CompletedTask;
        public async Task DisposeAsync() => await _h.DisposeAsync();

        [Fact(DisplayName = "Slot 40 min from Hanoi-now is bookable; 20 min is blocked (30-min guard)")]
        public async Task SlotStart_GuardUsesHanoiWallClock()
        {
            var hanoiNow = HanoiTime.HanoiNow();
            var slotStart40 = HanoiTime.SlotStart(_h.FutureDate, new TimeOnly(9, 40));
            var slotStart20 = HanoiTime.SlotStart(_h.FutureDate, new TimeOnly(9, 20));

            // 40 min ahead → passes the guard (slot bookable).
            Assert.True(slotStart40 >= hanoiNow.AddMinutes(30),
                "09:40 slot is 40 min from 09:00 Hanoi-now → bookable.");

            // 20 min ahead → inside the 30-min guard (blocked).
            Assert.True(slotStart20 < hanoiNow.AddMinutes(30),
                "09:20 slot is 20 min from 09:00 Hanoi-now → blocked.");

            // The actual 09:40 slot (seeded as SlotSoon40) should succeed:
            var ok = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>().CreateAsync(
                    new ModelViews.Booking.CreateBookingRequest(
                        _h.Court1, _h.FutureDate.ToString("yyyy-MM-dd"),
                        _h.SlotSoon40, 1, null, null,
                        Guid.NewGuid().ToString("N")),
                    _h.Alice));
            Assert.NotNull(ok);
        }

        [Fact(DisplayName = "Refund tiers: ≥2h → 100%, 1–2h → 50%, <1h → 0% (deterministic)")]
        public void RefundPolicy_Tiers()
        {
            // Pure policy check with fixed datetimes — no reliance on the
            // (static, per-host-pinned) HanoiTime clock, so it is deterministic.
            var now = new DateTime(2026, 10, 19, 9, 0, 0);

            var (r9h, _)   = Contract.Services.Payment.RefundPolicy.Tier(now, now.AddHours(9));
            var (r25h, _)  = Contract.Services.Payment.RefundPolicy.Tier(now, now.AddHours(2.5));
            var (r45m, _)  = Contract.Services.Payment.RefundPolicy.Tier(now, now.AddMinutes(45));
            var (r30m, _)  = Contract.Services.Payment.RefundPolicy.Tier(now, now.AddMinutes(30));

            Assert.Equal(1.0, r9h);   // ≥2h before start → 100%
            Assert.Equal(1.0, r25h);  // 2.5h → still 100%
            Assert.Equal(0.0, r45m);  // <1h → 0% (no-show tier)
            Assert.Equal(0.0, r30m);  // 30 min → 0%
        }

        [Fact(DisplayName = "Refund tier boundary: 50% exactly at 1–2h")]
        public void RefundTier_OneToTwoHoursIsFiftyPercent()
        {
            var now = new DateTime(2026, 10, 19, 9, 0, 0);
            var (r90m, _) = Contract.Services.Payment.RefundPolicy.Tier(now, now.AddMinutes(90));
            Assert.Equal(0.5, r90m);
        }

        [Fact(DisplayName = "The guard compares against Hanoi wall clock, not UTC (integration)")]
        public async Task Guard_UsesHanoiWallClock()
        {
            // HanoiTime is pinned by TestHost to FutureDate 09:00 Hanoi.
            // A slot 09:40 (40 min out) is bookable; 09:20 (20 min) is not.
            var hanoiNow = HanoiTime.HanoiNow();
            var ok40 = HanoiTime.SlotStart(_h.FutureDate, new TimeOnly(9, 40));
            var bad20 = HanoiTime.SlotStart(_h.FutureDate, new TimeOnly(9, 20));

            Assert.True(ok40 >= hanoiNow.AddMinutes(30), "09:40 is 40 min out → passes guard");
            Assert.True(bad20 < hanoiNow.AddMinutes(30), "09:20 is 20 min out → blocked");
        }
    }
}
