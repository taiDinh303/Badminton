using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using M.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BadmintonBE.API.Jobs
{
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
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var now = DateTime.UtcNow;
            var repo = uow.GetRepository<Booking>();
            var expired = await repo.GetByConditionAsync(b =>
                b.Status == BookingStatus.Pending
                && b.PendingExpiresAt.HasValue
                && b.PendingExpiresAt.Value <= now);
            if (expired.Count == 0) return;

            foreach (var b in expired)
            {
                b.Status = BookingStatus.Cancelled;
                b.CancelledAt = now;
                b.CancelReason = "Het han thanh toan (10 phut)";
                b.UpdatedAt = now;
                repo.Update(b);
            }
            await uow.SaveChangesAsync();
            _logger.LogInformation("Expired {Count} pending booking(s).", expired.Count);
        }
    }

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
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
}
