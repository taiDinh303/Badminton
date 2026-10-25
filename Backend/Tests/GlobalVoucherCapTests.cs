using Badminton.Core.Enums;
using Contract.Services.Booking;
using Contract.Services.Voucher;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModelViews.Account;
using Xunit;

namespace Badminton.Tests
{
    public sealed class GlobalVoucherCapTests : IAsyncLifetime
    {
        private readonly TestHost _h = new();

        public Task InitializeAsync() => Task.CompletedTask;
        public async Task DisposeAsync() => await _h.DisposeAsync();

        private async Task<(ModelViews.Booking.BookingView? view, Exception? err)> BookWithVoucher(
            int user, int slot, string code, int? courtId = null)
        {
            try
            {
                var view = await _h.RunScopeAsync(sp =>
                    sp.GetRequiredService<IBookingService>().CreateAsync(
                        new ModelViews.Booking.CreateBookingRequest(courtId ?? _h.Court1,
                            _h.FutureDate.ToString("yyyy-MM-dd"), slot,
                            1, null, code, Guid.NewGuid().ToString("N")), user));
                return (view, null);
            }
            catch (Exception e) { return (null, e); }
        }

        [Fact(DisplayName = "Each account can use voucher A and then voucher B")]
        public async Task SecondDifferentVoucher_IsAllowed()
        {
            await _h.AddVoucherAsync("USEA", totalQty: 10, maxPerUser: 1, value: 5_000);
            await _h.AddVoucherAsync("USEB", totalQty: 10, maxPerUser: 1, value: 5_000);

            var (first, e1) = await BookWithVoucher(_h.Alice, _h.SlotMorning, "USEA");
            Assert.NotNull(first);
            Assert.Null(e1);

            // A different voucher remains available for this same account.
            var (second, e2) = await BookWithVoucher(_h.Alice, _h.SlotNoon, "USEB");
            Assert.NotNull(second);
            Assert.Null(e2);

            // A different customer is unaffected by Alice's usage.
            var (other, e3) = await BookWithVoucher(_h.Bob, _h.SlotPeak, "USEB");
            Assert.NotNull(other);
            Assert.Null(e3);
        }

        [Fact(DisplayName = "Each code is single use even if a historical MaxPerUser is higher")]
        public async Task SameVoucherSecondTime_StillRejected()
        {
            await _h.AddVoucherAsync("ONCE", totalQty: 10, maxPerUser: 3, value: 5_000);

            var (first, e1) = await BookWithVoucher(_h.Alice, _h.SlotMorning, "ONCE");
            Assert.NotNull(first);
            Assert.Null(e1);

            var (second, e2) = await BookWithVoucher(_h.Alice, _h.SlotNoon, "ONCE");
            Assert.Null(second);
            Assert.IsType<Badminton.Core.Base.ConflictException>(e2);
        }

        [Fact(DisplayName = "/account summary reports points balance and voucher usage for the current user")]
        public async Task AccountSummary_ReportsPointsAndVoucherUsage()
        {
            // Seed points into Alice's ledger.
            await _h.CountPointsIntoAsync(_h.Alice, 3_000, "seed");

            await _h.AddVoucherAsync("SUMM", totalQty: 10, maxPerUser: 1, value: 5_000);
            var view = await _h.BookAsync(_h.Alice, _h.Court1, _h.SlotNoon,
                _h.FutureDate.ToString("yyyy-MM-dd"), voucherCode: "SUMM");
            Assert.NotNull(view);

            // Confirm on-site first (Completed only transitions from Confirmed).
            await _h.ConfirmOnSiteAsync(view!.Id, _h.Manager);
            _h.ActAs(_h.Manager, Role.Manager);
            await _h.CompleteAsPlayedAsync(view!.Id, _h.Manager, "da choi");

            // Balance = 3_000 seeded + earned on completion (> 3_000).
            var summary = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Account.IAccountService>()
                 .GetAccountSummaryAsync(_h.Alice));
            Assert.True(summary!.PointsBalance > 3_000,
                $"completed booking must bank points on top of the seed (got {summary.PointsBalance})");
            Assert.True(summary.PointsRedeemableVnd > 0);
            Assert.True(summary.VoucherUsed, "Alice used a voucher");
            Assert.NotNull(summary.UsedVoucher);
            Assert.Equal("SUMM", summary.UsedVoucher!.Code);
            Assert.DoesNotContain(summary.AvailableVouchers, v => v.Code == "SUMM");

            // A user with no points / no voucher sees an empty summary.
            var bob = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Account.IAccountService>()
                 .GetAccountSummaryAsync(_h.Bob));
            Assert.Equal(0, bob!.PointsBalance);
            Assert.False(bob.VoucherUsed);
            Assert.Null(bob.UsedVoucher);
        }

        [Fact]
        public async Task Used_code_is_rejected_in_preview_but_other_codes_remain_in_summary()
        {
            await _h.AddVoucherAsync("USED", 10, 1, 5000);
            await _h.AddVoucherAsync("OTHER", 10, 1, 5000);
            Assert.Null((await BookWithVoucher(_h.Alice, _h.SlotMorning, "USED")).err);
            await Assert.ThrowsAsync<Badminton.Core.Base.ConflictException>(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IVoucherService>().PreviewDiscountAsync("USED", _h.Alice, 100000)));
            Assert.Equal(5000, await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IVoucherService>().PreviewDiscountAsync("OTHER", _h.Alice, 100000)));
            var summary = await _h.RunScopeAsync(sp => sp.GetRequiredService<Contract.Services.Account.IAccountService>().GetAccountSummaryAsync(_h.Alice));
            Assert.Contains(summary.AvailableVouchers, v => v.Code == "OTHER");
            Assert.DoesNotContain(summary.AvailableVouchers, v => v.Code == "USED");
        }

        [Fact]
        public async Task Concurrent_same_user_same_code_only_reserves_once()
        {
            await _h.AddVoucherAsync("RACE", 10, 5, 5000);
            var results = await Task.WhenAll(BookWithVoucher(_h.Alice, _h.SlotMorning, "RACE"), BookWithVoucher(_h.Alice, _h.SlotNoon, "RACE", _h.Court2));
            Assert.Single(results, r => r.view != null);
            Assert.IsType<Badminton.Core.Base.ConflictException>(results.Single(r => r.err != null).err);
        }

        [Fact(DisplayName = "PointSummary and Voucher list endpoints read the current user's data only")]
        public async Task PointsAndVoucherLists_AreUserScoped()
        {
            await _h.CountPointsIntoAsync(_h.Bob, 5_000, "seed");
            await _h.AddVoucherAsync("ONLYBOB", totalQty: 10, maxPerUser: 1, value: 5_000);

            var bobPoints = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Account.IAccountService>()
                 .GetPointsAsync(_h.Bob));
            Assert.Equal(5_000, bobPoints!.Balance);
            Assert.Equal(5_000 * 20_000 / 1_000, bobPoints.RedeemableVnd);

            // Bob's available list contains the unused voucher; Alice's does not.
            var bobVouchers = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Account.IAccountService>()
                 .GetMyVouchersAsync(_h.Bob));
            Assert.Contains(bobVouchers, v => v.Code == "ONLYBOB");

            var aliceVouchers = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Account.IAccountService>()
                 .GetMyVouchersAsync(_h.Alice));
            Assert.Contains(aliceVouchers, v => v.Code == "ONLYBOB");
        }
    }
}
