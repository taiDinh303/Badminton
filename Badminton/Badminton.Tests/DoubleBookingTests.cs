using Contract.Services.Booking;
using Microsoft.Extensions.DependencyInjection;
using M.Core.Base;
using M.Core.Enums;
using ModelViews.Booking;
using Xunit;

namespace Badminton.Tests
{
    public class DoubleBookingTests : IAsyncLifetime
    {
        private readonly TestHost _h = new();

        public Task InitializeAsync() => Task.CompletedTask;
        public async Task DisposeAsync() => await _h.DisposeAsync();

        /// <summary>Attempt a booking in a fresh DI scope; never throws.</summary>
        private async Task<(BookingView? View, Exception? Err)> TryBook(
            int user, int court, int slot, DateOnly date, string? voucherCode = null)
        {
            try
            {
                var v = await _h.RunScopeAsync(sp =>
                    sp.GetRequiredService<IBookingService>().CreateAsync(
                        new CreateBookingRequest(court, date.ToString("yyyy-MM-dd"), slot,
                            1, null, voucherCode, Guid.NewGuid().ToString("N")), user));
                return (v, null);
            }
            catch (Exception e)
            {
                return (null, e);
            }
        }

        [Fact(DisplayName = "Two users booking the same court+slot: exactly one wins")]
        public async Task TwoUsersSameSlot_OnlyOneWins()
        {
            var date = _h.FutureDate;
            var slot = _h.SlotPeak;
            var court = _h.Court1;

            // Two reservations by two different users for the same
            // court/date/slot, each in its own DI scope (its own DbContext +
            // UnitOfWork + transaction), mirroring two simultaneous API calls.
            var (r1, e1) = await TryBook(_h.Alice, court, slot, date);
            var (r2, e2) = await TryBook(_h.Bob, court, slot, date);

            // Exactly one succeeded; the other hit SLOT_CONFLICT.
            Assert.Equal(1, (r1 is not null ? 1 : 0) + (r2 is not null ? 1 : 0));
            Assert.Equal(1, new[] { e1, e2 }.Count(e => e is ConflictException));

            // The database invariant: exactly ONE active booking in that slot.
            var activeInSlot = await _h.CountBookingsForSlotAsync(court, date, slot);
            Assert.Equal(1, activeInSlot);
        }

        [Fact(DisplayName = "True concurrency: parallel booking attempts never double-book")]
        public async Task ConcurrentBookings_NeverDoubleBook()
        {
            var date = _h.FutureDate;
            var slot = _h.SlotNoon;
            var court = _h.Court2;

            // Fire N simultaneous reservations for one slot; the filtered
            // unique index guarantees at most one active booking survives.
            // Each task swallows its own exception (SLOT_CONFLICT / daily
            // limit), and we assert the invariant on the DB afterward.
            int n = 8;
            var tasks = Enumerable.Range(0, n).Select(async _ =>
            {
                try
                {
                    await _h.RunScopeAsync(sp =>
                        sp.GetRequiredService<IBookingService>().CreateAsync(
                            new CreateBookingRequest(court, date.ToString("yyyy-MM-dd"), slot,
                                1, null, null, Guid.NewGuid().ToString("N")), _h.Alice));
                    return true;
                }
                catch (M.Core.Base.ConflictException)
                {
                    return false; // expected for losers
                }
            }).ToList();
            var results = await Task.WhenAll(tasks);

            // The invariant: exactly ONE active booking in that slot.
            var activeInSlot = await _h.CountBookingsForSlotAsync(court, date, slot);
            Assert.Equal(1, activeInSlot);
            // At most one concurrent attempt was the winner.
            Assert.True(results.Count(w => w) <= 1,
                "at most one concurrent booking can win the slot");
        }

        [Fact(DisplayName = "Same user booking 5 slots the same day is blocked at slot 5 (limit 4)")]
        public async Task DailySlotLimit_Enforced()
        {
            int booked = 0;
            // 5 distinct court+slot combos, all on the same date for one user.
            // The 4/day limit fires on the 5th attempt.
            var combos = new[]
            {
                (_h.Court1, _h.SlotMorning), (_h.Court1, _h.SlotNoon),
                (_h.Court1, _h.SlotPeak), (_h.Court2, _h.SlotMorning),
                (_h.Court2, _h.SlotNoon)
            };
            foreach (var (court, slot) in combos)
            {
                _h.ActAs(_h.Alice);
                var (v, e) = await TryBook(_h.Alice, court, slot, _h.FutureDate);
                if (e is ConflictException && e.Message.Contains("4 slot/ngay")) break;
                booked += v is not null ? 1 : 0;
            }
            Assert.Equal(4, booked);
        }
    }
}
