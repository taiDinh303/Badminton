using Contract.Services.Booking;
using Contract.Services.Payment;
using Microsoft.Extensions.DependencyInjection;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Xunit;

namespace Badminton.Tests
{
    public class LifecycleAndRedeemTests : IAsyncLifetime
    {
        private readonly TestHost _h = new();

        public Task InitializeAsync() => Task.CompletedTask;
        public async Task DisposeAsync() => await _h.DisposeAsync();

        [Fact(DisplayName = "Manager marks a Confirmed booking Completed; re-marking is a conflict")]
        public async Task MarkCompleted_TransitionsConfirmedToCompleted()
        {
            var view = await _h.MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotNoon);

            _h.ActAs(_h.Manager, Role.Manager);
            var completed = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>()
                 .MarkCompletedAsync(view!.Id, _h.Manager, "da choi"));
            Assert.Equal("Completed", completed.Status);

            // DEC-01: completing the booking banks its points.
            var balance = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                 .GetBalanceAsync(_h.Alice));
            Assert.True(balance > 0, "completing a paid booking must bank points");

            // Re-marking a Completed booking must fail.
            Exception? err = null;
            try
            {
                await _h.RunScopeAsync(sp =>
                    sp.GetRequiredService<IBookingService>()
                     .MarkCompletedAsync(view!.Id, _h.Manager));
            }
            catch (Exception e) { err = e; }
            Assert.IsType<Badminton.Core.Base.ConflictException>(err);
        }

        [Fact(DisplayName = "3 no-shows in one month block the customer next month (Q2=A, DEC-03)")]
        public async Task ThreeNoShows_InAMonth_BookingsBlocked()
        {
            var slots = new[] { _h.SlotMorning, _h.SlotNoon, _h.SlotPeak };
            var starts = new[] { new TimeOnly(10, 0), new TimeOnly(12, 0), new TimeOnly(18, 0) };
            var hostNowUtc = HanoiTime.NowUtc; // the default 09:00 Hanoi pin
            var offset = HanoiTime.FixedOffset;

            for (var i = 0; i < 3; i++)
            {
                var b = await _h.MakeConfirmedBookingAsync(_h.Bob, _h.Court1, slots[i]);

                // 1 minute past this slot's start → the no-show guard passes.
                var targetHanoi = HanoiTime.SlotStart(_h.FutureDate, starts[i]).AddMinutes(1);
                var utcInstant = targetHanoi - offset;
                HanoiTime.NowUtc = () => DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);
                _h.ActAs(_h.Manager, Role.Manager);
                await _h.RunScopeAsync(sp =>
                    sp.GetRequiredService<IBookingService>()
                     .MarkNoShowAsync(b!.Id, _h.Manager));

                HanoiTime.NowUtc = hostNowUtc; // back to default for next booking
            }

            var bob = await _h.GetUserAsync(_h.Bob);
            Assert.Equal(3, bob.NoShowCount);
            Assert.True(bob.NoShowLimited);
            Assert.NotNull(bob.NoShowLimitedPeriod);

            // Inside the blocked month the customer cannot book again.
            var savedNow = HanoiTime.NowUtc;
            try
            {
                var blockedMonth = bob.NoShowLimitedPeriod!.Value;
                HanoiTime.NowUtc = () => new DateTime(
                    blockedMonth.Year, blockedMonth.Month, 15, 2, 0, 0,
                    DateTimeKind.Utc); // 09:00 Hanoi on the 15th of blocked month
                var bookDate = blockedMonth.AddDays(20);
                Exception? blocked = null;
                try
                {
                    await _h.BookAsync(_h.Bob, _h.Court1, _h.SlotPeak,
                        bookDate.ToString("yyyy-MM-dd"));
                }
                catch (Exception e) { blocked = e; }
                Assert.IsType<Badminton.Core.Base.ConflictException>(blocked);
                Assert.Contains("gioi han", blocked!.Message,
                    System.StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                HanoiTime.NowUtc = savedNow;
            }
        }

        [Fact(DisplayName = "DEC-03: a no-show before the slot has started is rejected")]
        public async Task MarkNoShow_BeforeSlotStart_IsRejected()
        {
            var view = await _h.MakeConfirmedBookingAsync(_h.Bob, _h.Court1, _h.SlotPeak);
            // Pinned host clock is 09:00 Hanoi; SlotPeak starts 18:00 → 9h out.
            _h.ActAs(_h.Manager, Role.Manager);
            var err = await Record.ExceptionAsync(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>()
                 .MarkNoShowAsync(view!.Id, _h.Manager)));
            Assert.IsType<Badminton.Core.Base.ConflictException>(err);
        }

        [Fact(DisplayName = "Redeeming 5,000 points deducts 100k VND and 5,000 pts from balance (Q4=A)")]
        public async Task RedeemPoints_DeductsBalanceAndLowersPayable()
        {
            await _h.CountPointsIntoAsync(_h.Bob, 5_000, "seed");

            // Peak court 1 = 150_000.  5,000 pts → 100,000 VND off → 50,000 payable.
            var withPoints = await _h.BookAsync(_h.Bob, _h.Court1, _h.SlotPeak,
                _h.FutureDate.ToString("yyyy-MM-dd"), pointsToRedeem: 5_000);

            Assert.Equal(5_000, withPoints!.PointsRedeemed);
            Assert.Equal(100_000, withPoints.Price.PointsDiscount);
            Assert.Equal(150_000 - 100_000, withPoints.PayableAmount);

            var balance = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                 .GetBalanceAsync(_h.Bob));
            Assert.Equal(0, balance); // 5,000 seeded − 5,000 redeemed
        }

        [Fact(DisplayName = "A full refund restores redeemed points (Q4=A mirror, DEC-02)")]
        public async Task FullRefund_RestoresRedeemedPoints()
        {
            await _h.CountPointsIntoAsync(_h.Carol, 3_000, "seed");
            var view = await _h.BookAsync(_h.Carol, _h.Court1, _h.SlotNoon,
                _h.FutureDate.ToString("yyyy-MM-dd"), pointsToRedeem: 1_000);

            _h.ActAs(_h.Manager, Role.Manager);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>()
                 .ConfirmOnSiteAsync(view!.Id, "Cash", _h.Manager));
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>()
                 .RefundAsync(view!.Id, _h.Manager, "full refund test"));

            var balance = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                 .GetBalanceAsync(_h.Carol));
            Assert.Equal(3_000, balance);
        }

        [Fact(DisplayName = "Manager of a different branch cannot refund another branch's booking (Q3=A)")]
        public async Task BranchScope_ManagerCannotTouchOtherBranch()
        {
            var view = await _h.MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotNoon);

            _h.ActAs(_h.ManagerOtherBranch, Role.Manager);
            Exception? err = null;
            try
            {
                await _h.RunScopeAsync(sp =>
                    sp.GetRequiredService<IPaymentService>()
                     .RefundAsync(view!.Id, _h.ManagerOtherBranch, "cross-branch"));
            }
            catch (Exception e) { err = e; }

            Assert.IsType<Badminton.Core.Base.ConflictException>(err);
            Assert.Contains("cơ sở", err!.Message, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
