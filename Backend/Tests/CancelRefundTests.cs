using Contract.Repositories.Entity;
using Contract.Services.Booking;
using Contract.Services.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Badminton.Core.Enums;
using Xunit;

namespace Badminton.Tests
{
    public class CancelRefundTests : IAsyncLifetime
    {
        private readonly TestHost _h = new();

        public Task InitializeAsync() => Task.CompletedTask;
        public async Task DisposeAsync() => await _h.DisposeAsync();

        private async Task<int> MakeConfirmedBookingAsync(int user, int court, int slot)
        {
            var view = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>().CreateAsync(
                    new ModelViews.Booking.CreateBookingRequest(court,
                        _h.FutureDate.ToString("yyyy-MM-dd"), slot,
                        1, null, null, Guid.NewGuid().ToString("N")), user));
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>()
                 .ConfirmOnSiteAsync(view!.Id, "Cash", _h.Manager));
            return view!.Id;
        }

        [Fact(DisplayName = "P0-1 + DEC-01/02: owner cancels a COMPLETED paid booking → 100% refund, points net 0")]
        public async Task CancelPaidBooking_TriggersTieredRefund()
        {
            var bookingId = await MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotPeak);

            // DEC-01: confirming on-site banks NO points.
            var balanceAfterConfirm = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                 .GetBalanceAsync(_h.Alice));
            Assert.Equal(0, balanceAfterConfirm);

            // Complete the booking → the earn row is banked now.
            _h.ActAs(_h.Manager, Role.Manager);
            await _h.CompleteAsPlayedAsync(bookingId, _h.Manager, "da choi");
            var balanceAtCompleted = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                 .GetBalanceAsync(_h.Alice));
            Assert.True(balanceAtCompleted > 0, "completed booking must have earned points");

            // Owner cancels her own paid order (9h out → 100% tier).
            _h.ActAs(_h.Alice, Role.Customer);
            var view = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>()
                 .CancelAsync(bookingId, _h.Alice));
            Assert.Equal("Cancelled", view!.Status);

            // Exactly one CANCEL-REFUND row, Refunded, for the full amount.
            var refundRow = await _h.RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<Repositories.Context.BadmintonBookingDbContext>();
                return db.Payments.FirstOrDefaultAsync(p =>
                    p.GatewayRef == $"CANCEL-REFUND-{bookingId}");
            })!;
            Assert.NotNull(refundRow);
            Assert.Equal(PaymentStatus.Refunded, refundRow!.Status);
            Assert.True(refundRow.RefundAmount > 0, "a ≥2h cancel must refund money");

            // DEC-02: the full-refund clawback reverses the DEC-01 earn → net 0.
            var balanceAfter = await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<Contract.Services.Points.IPointService>()
                 .GetBalanceAsync(_h.Alice));
            Assert.Equal(0, balanceAfter);

            // Slot released: no active booking in that slot.
            var active = await _h.CountBookingsForSlotAsync(_h.Court1, _h.FutureDate, _h.SlotPeak);
            Assert.Equal(0, active);
        }

        [Fact(DisplayName = "P0-1: re-cancelling a paid booking is idempotent (no second refund row)")]
        public async Task CancelPaidBooking_IsIdempotent()
        {
            var bookingId = await MakeConfirmedBookingAsync(_h.Bob, _h.Court2, _h.SlotPeak);

            _h.ActAs(_h.Bob, Role.Customer);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>().CancelAsync(bookingId, _h.Bob));

            _h.ActAs(_h.Manager, Role.Manager);
            await _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>()
                 .CancelRefundAsync(bookingId, _h.Manager));

            var rows = await _h.RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<Repositories.Context.BadmintonBookingDbContext>();
                return db.Payments.CountAsync(p =>
                    p.GatewayRef == $"CANCEL-REFUND-{bookingId}");
            });
            Assert.Equal(1, rows);
        }

        [Fact(DisplayName = "P0-2: a non-owner customer cannot cancel someone else's booking")]
        public async Task ForeignCustomer_CannotCancel()
        {
            var bookingId = await MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotPeak);

            _h.ActAs(_h.Bob, Role.Customer);
            var err = await Record.ExceptionAsync(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>().CancelAsync(bookingId, _h.Bob)));

            Assert.IsType<Badminton.Core.Base.ConflictException>(err);
        }

        [Fact(DisplayName = "P0-4/SEC-02: a manager from another branch cannot cancel this branch's booking")]
        public async Task OtherBranchManager_CannotCancel()
        {
            var bookingId = await MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotPeak);

            _h.ActAs(_h.ManagerOtherBranch, Role.Manager);
            var err = await Record.ExceptionAsync(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>().CancelAsync(bookingId, _h.ManagerOtherBranch)));

            Assert.IsType<Badminton.Core.Base.ConflictException>(err);
        }

        [Fact(DisplayName = "P0-4/SEC-02: a manager WITHOUT an assigned branch is rejected (fail-closed)")]
        public async Task NullBranchManager_CannotRefund()
        {
            var bookingId = await MakeConfirmedBookingAsync(_h.Alice, _h.Court1, _h.SlotPeak);

            // A manager row whose BranchId is null → fail-closed on refund/cancel.
            int nullBranchManager = 0;
            await _h.RunScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<Repositories.Context.BadmintonBookingDbContext>();
                var m = new User
                {
                    FullName = "MgrNoBranch",
                    PhoneNumber = "0900000099",
                    PasswordHash = "x",
                    Role = Role.Manager,
                    BranchId = null,
                    Status = UserStatus.Active,
                    IsVerified = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.Users.Add(m);
                await db.SaveChangesAsync();
                nullBranchManager = m.Id;
            });

            _h.ActAs(nullBranchManager, Role.Manager);
            var err = await Record.ExceptionAsync(() => _h.RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>()
                 .CancelRefundAsync(bookingId, nullBranchManager)));

            Assert.IsType<Badminton.Core.Base.ConflictException>(err);
        }
    }
}
