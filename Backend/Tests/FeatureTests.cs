using Contract.Repositories.Entity;
using Contract.Services.Booking;
using Contract.Services.Membership;
using Contract.Services.Notify;
using Contract.Services.Waitlist;
using Badminton.Core.Enums;
using Badminton.Core.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Repositories.Context;

namespace Badminton.Tests
{
    public class FeatureTests : IAsyncLifetime
    {
        private readonly TestHost _h = new();
        public Task InitializeAsync() => Task.CompletedTask;
        public async Task DisposeAsync() => await _h.DisposeAsync();

        // ── FEAT 1: Notify outbox fan-out ─────────────────────────────────

        [Fact(DisplayName = "Confirming a booking enqueues a notification outbox row (log channel)")]
        public async Task Notify_Confirm_EnqueuesOutboxRow()
        {
            var view = await _h.MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotNoon);

            var rows = await _h.RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                return db.Notifications
                    .Where(n => n.BookingId == view!.Id
                        && n.Event == NotifyEvent.BookingConfirmed)
                    .ToListAsync();
            });

            // Log channel is the only one registered in TestHost → 1 row.
            Assert.Single(rows);
            Assert.Equal(NotifyStatus.Delivered, rows[0].Status);
            Assert.Equal("log", rows[0].Channel);
        }

        [Fact(DisplayName = "Re-confirming the same booking does NOT create a duplicate notification row")]
        public async Task Notify_ReplayIsIdempotent()
        {
            var view = await _h.MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotNoon);

            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<INotifyService>()
                 .NotifyBookingConfirmedAsync(view!.Id));

            var count = await _h.RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                return db.Notifications
                    .CountAsync(n => n.BookingId == view!.Id
                        && n.Event == NotifyEvent.BookingConfirmed);
            });
            Assert.Equal(1, count);
        }

        // ── FEAT 3: QR check-in ──────────────────────────────────────────

        [Fact(DisplayName = "A confirmed booking has a QR token; check-in records CheckedInAt")]
        public async Task Qr_ConfirmedBookingHasToken_CheckinRecords()
        {
            var view = await _h.MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotNoon);

            var booking = await _h.GetBookingAsync(view!.Id);
            Assert.NotNull(booking.QrToken);
            Assert.Null(booking.CheckedInAt);

            // Staff scans the QR.
            _h.ActAs(_h.Manager, Role.Manager);
            var checkedIn = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<ICheckinService>()
                 .CheckinByTokenAsync(booking.QrToken!, _h.Manager));

            Assert.Equal(booking.Id, checkedIn.Id);
            Assert.NotNull(checkedIn.CheckedInAt);

            // Idempotent: scanning again returns the same booking, no error.
            var again = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<ICheckinService>()
                 .CheckinByTokenAsync(booking.QrToken!, _h.Manager));
            Assert.Equal(booking.Id, again.Id);
        }

        [Fact(DisplayName = "Check-in by booking code works for a pending booking")]
        public async Task Qr_CheckinByCode()
        {
            var view = await _h.BookAsync(_h.Bob, _h.Court2, _h.SlotPeak,
                _h.FutureDate.ToString("yyyy-MM-dd"));

            _h.ActAs(_h.Manager, Role.Manager);
            var checkedIn = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<ICheckinService>()
                 .CheckinByCodeAsync(view!.BookingCode, _h.Manager));

            Assert.NotNull(checkedIn.CheckedInAt);
        }

        // ── FEAT 4: Membership tier multiplier ───────────────────────────

        [Fact(DisplayName = "Gold member earns 2x points on a completed booking")]
        public async Task Membership_Gold_2xPoints()
        {
            // Assign Gold tier to Carol.
            _h.ActAs(_h.Manager, Role.Admin);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IMembershipService>()
                 .AssignTierAsync(_h.Carol, MembershipTier.Gold, _h.Manager));

            var view = await _h.MakeConfirmedBookingAsync(_h.Carol, _h.Court1, _h.SlotNoon);

            // Complete it → points banked at 2x multiplier.
            _h.ActAs(_h.Manager, Role.Manager);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>()
                 .MarkCompletedAsync(view!.Id, _h.Manager));

            var balance = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                 .GetBalanceAsync(_h.Carol));

            var payable = view.PayableAmount;
            Assert.Equal(payable * 2, balance); // 2x Gold multiplier
        }

        [Fact(DisplayName = "Silver member earns 1.5x points on a completed booking")]
        public async Task Membership_Silver_15xPoints()
        {
            _h.ActAs(_h.Manager, Role.Admin);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IMembershipService>()
                 .AssignTierAsync(_h.Bob, MembershipTier.Silver, _h.Manager));

            var view = await _h.MakeConfirmedBookingAsync(_h.Bob, _h.Court2, _h.SlotPeak);

            _h.ActAs(_h.Manager, Role.Manager);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>()
                 .MarkCompletedAsync(view!.Id, _h.Manager));

            var balance = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                 .GetBalanceAsync(_h.Bob));

            var payable = view.PayableAmount;
            Assert.Equal((int)Math.Round(payable * 1.5m), balance);
        }

        [Fact(DisplayName = "Non-member (None tier) earns 1x points")]
        public async Task Membership_None_1xPoints()
        {
            var view = await _h.MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotNoon);

            _h.ActAs(_h.Manager, Role.Manager);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>()
                 .MarkCompletedAsync(view!.Id, _h.Manager));

            var balance = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                 .GetBalanceAsync(_h.Alice));

            Assert.Equal(view.PayableAmount, balance);
        }

        // ── FEAT 2: Waitlist join + promote ──────────────────────────────

        [Fact(DisplayName = "Joining the waitlist on a full slot creates a Waiting entry")]
        public async Task Waitlist_Join_FullSlot()
        {
            // Fill the slot first.
            await _h.MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotNoon);

            // Bob joins the waitlist for the same slot.
            var entry = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IWaitlistService>()
                 .JoinAsync(_h.Bob, _h.Court1, _h.SlotNoon,
                     _h.FutureDate, 1, "vao danh sach cho"));

            Assert.Equal(WaitlistStatus.Waiting, entry.Status);
        }

        [Fact(DisplayName = "Promoting a freed slot creates a Pending booking for the first waiter")]
        public async Task Waitlist_Promote_CreatesBooking()
        {
            // Alice fills the slot (confirmed booking).
            var aliceView = await _h.MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotNoon);
            // Bob joins the waitlist for that full slot.
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IWaitlistService>()
                 .JoinAsync(_h.Bob, _h.Court1, _h.SlotNoon, _h.FutureDate, 1, null));

            // Alice cancels her own booking → the slot frees up.
            _h.ActAs(_h.Alice, Role.Customer);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>()
                 .CancelAsync(aliceView!.Id, _h.Alice));

            // Promote → creates a Pending booking for Bob (the waiter).
            var promoted = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IWaitlistService>()
                 .PromoteAsync(_h.Court1, _h.SlotNoon, _h.FutureDate));

            Assert.NotNull(promoted);
            Assert.Equal(_h.Bob, promoted!.UserId);
            Assert.Equal(BookingStatus.Pending, promoted.Status);
        }

        [Fact(DisplayName = "Joining a non-full slot is rejected")]
        public async Task Waitlist_Join_NonFullSlot_Throws()
        {
            // No one has booked the slot yet.
            Exception? err = null;
            try
            {
                await _h.RunScopeAsync(sp =>
                    sp.GetRequiredService<IWaitlistService>()
                     .JoinAsync(_h.Alice, _h.Court2, _h.SlotPeak,
                         _h.FutureDate, 1, null));
            }
            catch (Exception e) { err = e; }
            Assert.IsType<Badminton.Core.Base.BadRequestException>(err);
        }
    }
}
