using Contract.Services.Booking;
using Contract.Services.Voucher;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using ModelViews.Booking;
using Xunit;

namespace Badminton.Tests
{
    public class VoucherExhaustionTests : IAsyncLifetime
    {
        private readonly TestHost _h = new();

        public Task InitializeAsync() => Task.CompletedTask;
        public async Task DisposeAsync() => await _h.DisposeAsync();

        private async Task<(BookingView? View, Exception? Err)> TryVoucherBook(
            int user, int court, int slot, string code)
        {
            try
            {
                var v = await _h.RunScopeAsync(sp =>
                    sp.GetRequiredService<IBookingService>().CreateAsync(
                        new CreateBookingRequest(court, _h.FutureDate.ToString("yyyy-MM-dd"),
                            slot, 1, null, code, Guid.NewGuid().ToString("N")), user));
                return (v, null);
            }
            catch (Exception e)
            {
                return (null, e);
            }
        }

        [Fact(DisplayName = "6 concurrent reservations for a TotalQty=3 voucher never over-reserve")]
        public async Task ConcurrentReservations_NeverOverReserve()
        {
            var voucher = await _h.AddVoucherAsync("RACE", totalQty: 3, maxPerUser: 100, value: 5_000);
            var extraUsers = await _h.RunScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<Repositories.Context.BadmintonBookingDbContext>();
                var users = Enumerable.Range(4, 3).Select(i => new Contract.Repositories.Entity.User {
                    FullName = $"Voucher racer {i}", PhoneNumber = $"090000001{i}", PasswordHash = "unused", IsVerified = true
                }).ToList();
                db.Users.AddRange(users); await db.SaveChangesAsync(); return users.Select(u => u.Id).ToArray();
            });

            var combos = new[]
            {
                (_h.Court1, _h.SlotMorning, _h.Alice),
                (_h.Court1, _h.SlotNoon,   _h.Bob),
                (_h.Court1, _h.SlotPeak,   _h.Carol),
                (_h.Court2, _h.SlotMorning, extraUsers[0]),
                (_h.Court2, _h.SlotNoon,   extraUsers[1]),
                (_h.Court2, _h.SlotPeak,   extraUsers[2]),
            };

            var results = await Task.WhenAll(combos.Select(async c =>
            {
                try
                {
                    var v = await _h.RunScopeAsync(sp =>
                        sp.GetRequiredService<IBookingService>().CreateAsync(
                            new CreateBookingRequest(c.Item1, _h.FutureDate.ToString("yyyy-MM-dd"),
                                c.Item2, 1, null, "RACE", Guid.NewGuid().ToString("N")), c.Item3));
                    return (true, (Exception?)null, v);
                }
                catch (Exception e)
                {
                    return (false, e, (ModelViews.Booking.BookingView?)null);
                }
            }));

            int wins = results.Count(r => r.Item1);
            int exhausted = results.Count(r =>
                r.Item2 is ConflictException ce && ce.Message.Contains("het luot"));
            var after = await _h.GetVoucherAsync("RACE");

            // SAFETY INVARIANTS:
            Assert.True(after.UsedQty <= after.TotalQty,
                $"counter exceeded its cap (UsedQty={after.UsedQty}, TotalQty={after.TotalQty})");
            Assert.True(wins <= after.TotalQty,
                $"more bookings than the voucher cap ({wins} > {after.TotalQty})");
            // With 6 attempts for 3 slots, at least some must be exhausted.
            Assert.True(exhausted >= 1, "expected at least one 'hết lượt' conflict");
            // The counter must be consistent with how many reservations held.
            Assert.True(after.UsedQty >= wins,
                $"counter ({after.UsedQty}) should be at least the successful bookings ({wins})");
        }

        [Fact(DisplayName = "A second booking with a used-up voucher is rejected")]
        public async Task UsedUpVoucher_SecondBookingRejected()
        {
            await _h.AddVoucherAsync("ONCE", totalQty: 1, maxPerUser: 100, value: 5_000);

            var (first, e1) = await TryVoucherBook(_h.Alice, _h.Court1, _h.SlotMorning, "ONCE");
            Assert.NotNull(first);
            Assert.Null(e1);

            var (second, e2) = await TryVoucherBook(_h.Bob, _h.Court1, _h.SlotNoon, "ONCE");
            Assert.Null(second);
            Assert.IsType<ConflictException>(e2);

            var after = await _h.GetVoucherAsync("ONCE");
            Assert.Equal(1, after.UsedQty);
        }

        [Fact(DisplayName = "MaxPerUser: the same user cannot use the same voucher twice")]
        public async Task MaxPerUser_SameUserSecondTimeRejected()
        {
            await _h.AddVoucherAsync("PERUSER", totalQty: 100, maxPerUser: 1, value: 5_000);

            var (first, e1) = await TryVoucherBook(_h.Alice, _h.Court1, _h.SlotMorning, "PERUSER");
            Assert.NotNull(first);
            Assert.Null(e1);

            // Alice tries the same voucher on a different slot → per-user cap.
            var (second, e2) = await TryVoucherBook(_h.Alice, _h.Court1, _h.SlotNoon, "PERUSER");
            Assert.Null(second);
            Assert.IsType<ConflictException>(e2);
            Assert.Contains("Bạn đã sử dụng mã voucher này", e2!.Message);

            // A different user is unaffected by Alice's per-user cap.
            var (third, e3) = await TryVoucherBook(_h.Bob, _h.Court1, _h.SlotNoon, "PERUSER");
            Assert.NotNull(third);
            Assert.Null(e3);
        }

        [Fact(DisplayName = "Cancelling a pre-payment booking releases its voucher slot")]
        public async Task CancelReleasesVoucher()
        {
            await _h.AddVoucherAsync("REL", totalQty: 1, maxPerUser: 100, value: 5_000);

            var booking = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>().CreateAsync(
                    new CreateBookingRequest(_h.Court1, _h.FutureDate.ToString("yyyy-MM-dd"),
                        _h.SlotMorning, 1, null, "REL", Guid.NewGuid().ToString("N")), _h.Alice));
            var usedAfterReserve = await _h.GetVoucherAsync("REL");
            Assert.Equal(1, usedAfterReserve.UsedQty);

            // Cancel the pending booking (same user).
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>()
                 .CancelAsync(booking!.Id, _h.Alice));
            var afterRelease = await _h.GetVoucherAsync("REL");
            Assert.True(afterRelease.UsedQty == 0, "releasing the cancel must return the slot to the pool");

            // Another user can now use it.
            var (other, eOther) = await TryVoucherBook(_h.Bob, _h.Court1, _h.SlotNoon, "REL");
            Assert.NotNull(other);
            Assert.Null(eOther);
        }
    }
}
