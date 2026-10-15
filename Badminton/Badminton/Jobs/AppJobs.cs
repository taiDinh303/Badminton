using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using M.Core.Enums;
using M.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BadmintonBE.API.Jobs
{
    /// <summary>
    /// Expire pending bookings after 10 minutes (docs 01 §4).
    /// ALSO releases any reserved voucher slot and restores any redeemed
    /// points so the pool/balance stays consistent.
    /// </summary>
    public sealed class PendingBookingExpiryJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<PendingBookingExpiryJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        public PendingBookingExpiryJob(IServiceScopeFactory scopes,
            ILogger<PendingBookingExpiryJob> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync();
                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync()
        {
            using var scope = _scopes.CreateScope();
            var sp = scope.ServiceProvider;
            var uow = sp.GetRequiredService<IUnitOfWork>();
            var now = DateTime.UtcNow;

            var repo = uow.GetRepository<Booking>();
            var expired = await repo.GetByConditionAsync(b =>
                b.Status == BookingStatus.Pending
                && b.PendingExpiresAt.HasValue
                && b.PendingExpiresAt.Value <= now);
            if (expired.Count == 0) return;

            // Release vouchers + restore points for each expired booking.
            var voucherRepo = uow.GetRepository<VoucherRedemption>();
            var pointRepo = uow.GetRepository<PointLedger>();

            foreach (var b in expired)
            {
                b.Status = BookingStatus.Cancelled;
                b.CancelledAt = now;
                b.CancelReason = "Het han thanh toan (10 phut)";
                b.UpdatedAt = now;
                repo.Update(b);

                // Release voucher slot.
                if (b.VoucherRedemptionId is { } rid)
                {
                    var redemption = (await voucherRepo.GetByConditionAsync(x => x.Id == rid))
                        .FirstOrDefault();
                    if (redemption is not null)
                    {
                        var v = await uow.GetRepository<Voucher>().GetByIdAsync(redemption.VoucherId);
                        if (v is not null)
                        {
                            // Decrement UsedQty (guarded).
                            await uow.ExecuteSqlRawAsync(
                                "UPDATE Vouchers SET UsedQty = UsedQty - 1 " +
                                "WHERE Id = @p0 AND UsedQty > 0", v.Id);
                            await voucherRepo.RemoveAsync(redemption);
                        }
                    }
                }

                // Restore redeemed points (ledger row with positive delta).
                if (b.PointsRedeemed > 0)
                {
                    var alreadyRestored = await pointRepo.CountByConditionAsync(
                        p => p.BookingId == b.Id && p.LedgerKey == $"restore:{b.Id}");
                    if (alreadyRestored == 0)
                    {
                        _ = pointRepo.AddAsync(new PointLedger
                        {
                            UserId = b.UserId,
                            Delta = b.PointsRedeemed,
                            BookingId = b.Id,
                            Reason = $"auto-expire restore of {b.PointsRedeemed} pts ({b.BookingCode})",
                            LedgerKey = $"restore:{b.Id}"
                        });
                    }
                }
            }

            await uow.SaveChangesAsync();
            _logger.LogInformation("Expired {Count} pending booking(s), released vouchers + restored points.", expired.Count);
        }
    }

    /// <summary>
    /// Auto-complete: after a Confirmed booking's slot END time has passed
    /// (in Hanoi wall clock), mark it Completed so the customer can review it.
    /// Runs every minute.
    /// </summary>
    public sealed class AutoCompleteJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AutoCompleteJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

        public AutoCompleteJob(IServiceScopeFactory scopes,
            ILogger<AutoCompleteJob> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync();
                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync()
        {
            using var scope = _scopes.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var hanoiNow = HanoiTime.HanoiNow();

            var bookings = await uow.GetRepository<Booking>()
                .GetByConditionAsync(b => b.Status == BookingStatus.Confirmed);

            var slotRepo = uow.GetRepository<TimeSlot>();
            var nowUtc = DateTime.UtcNow;
            int completed = 0;

            foreach (var b in bookings)
            {
                var slot = await slotRepo.GetByIdAsync(b.SlotId);
                if (slot is null) continue;
                var slotEnd = HanoiTime.SlotEnd(b.Date, slot.EndTime);
                if (slotEnd <= hanoiNow)
                {
                    b.Status = BookingStatus.Completed;
                    b.UpdatedAt = nowUtc;
                    uow.GetRepository<Booking>().Update(b);
                    completed++;
                }
            }

            if (completed > 0)
            {
                await uow.SaveChangesAsync();
                _logger.LogInformation("Auto-completed {Count} confirmed booking(s) past slot end.", completed);
            }
        }
    }

    /// <summary>
    /// Deactivate expired vouchers (hourly).
    /// </summary>
    public sealed class VoucherExpiryJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<VoucherExpiryJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        public VoucherExpiryJob(IServiceScopeFactory scopes,
            ILogger<VoucherExpiryJob> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync();
                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync()
        {
            using var scope = _scopes.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var today = HanoiTime.HanoiToday();
            var repo = uow.GetRepository<Voucher>();
            var expired = await repo.GetByConditionAsync(v =>
                v.IsActive && v.ValidUntil.HasValue && v.ValidUntil.Value <= today);
            if (expired.Count == 0) return;

            foreach (var v in expired)
            {
                v.IsActive = false;
                v.UpdatedAt = DateTime.UtcNow;
                repo.Update(v);
            }
            await uow.SaveChangesAsync();
            _logger.LogInformation("Deactivated {Count} expired voucher(s).", expired.Count);
        }
    }

    /// <summary>
    /// Resets the no-show booking block at the start of each Hanoi month
    /// (docs 01 §4: "mỗi khách tích 3 lần no-show trong tháng thì bị
    /// giới hạn đặt trong tháng kế tiếp" → unblocks on the 1st of the
    /// following month). Runs daily; idempotent.
    /// </summary>
    public sealed class NoShowResetJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<NoShowResetJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        public NoShowResetJob(IServiceScopeFactory scopes,
            ILogger<NoShowResetJob> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync();
                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync()
        {
            using var scope = _scopes.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var currentMonthStart = HanoiTime.HanoiToday().MonthStart();

            // All users currently blocked whose blocked-month has passed.
            var users = await uow.GetRepository<User>().GetByConditionAsync(u =>
                u.NoShowLimited && u.NoShowLimitedPeriod != null);

            int reset = 0;
            foreach (var u in users)
            {
                if (u.NoShowLimitedPeriod is { } period && currentMonthStart > period)
                {
                    u.NoShowLimited = false;
                    u.NoShowLimitedPeriod = null;
                    u.UpdatedAt = DateTime.UtcNow;
                    uow.GetRepository<User>().Update(u);
                    reset++;
                }
            }

            if (reset > 0)
            {
                await uow.SaveChangesAsync();
                _logger.LogInformation("Unblocked {Count} user(s) whose no-show limit period ended.", reset);
            }
        }
    }
}
