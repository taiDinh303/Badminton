using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Waitlist;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Badminton.API.Jobs
{
    public sealed class WaitlistPromotionJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<WaitlistPromotionJob> _logger;
        private readonly JobLock _lock;
        private readonly JobHeartbeatRegistry _heartbeat;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(2);
        private const string LockKey = nameof(WaitlistPromotionJob);

        public WaitlistPromotionJob(IServiceScopeFactory scopes,
            ILogger<WaitlistPromotionJob> logger,
            JobLock jobLock,
            JobHeartbeatRegistry heartbeat)
        {
            _scopes = scopes;
            _logger = logger;
            _lock = jobLock;
            _heartbeat = heartbeat;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync();
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex,
                        "{Job} tick failed; will retry on the next interval.",
                        nameof(WaitlistPromotionJob));
                }
                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync()
        {
            if (!await _lock.AcquireAsync(LockKey))
            {
                _logger.LogDebug("{Job}: another instance owns this tick; skipping.",
                    nameof(WaitlistPromotionJob));
                return;
            }
            try
            {
                _heartbeat.Record(LockKey);
                using var scope = _scopes.CreateScope();
                var sp = scope.ServiceProvider;
                await sp.GetRequiredService<Contract.Services.Notify.INotifyService>().RetryPendingAsync();
                await sp.GetRequiredService<Contract.Services.Operations.IReminderService>().RunAsync();
                await sp.GetRequiredService<Contract.Services.Operations.IClubMaintenanceService>().RunAsync();
                var waitlist = sp.GetRequiredService<IWaitlistService>();
                var uow = sp.GetRequiredService<IUnitOfWork>();
                var today = HanoiTime.HanoiToday();
                await waitlist.ExpirePastEntriesAsync();

                // Distinct full-tuples with waiting candidates.
                var entries = await uow.GetRepository<WaitlistEntry>()
                    .GetByConditionAsync(w =>
                        w.Status == WaitlistStatus.Waiting && w.Date >= today);
                if (entries.Count == 0)
                    return;

                var tuples = entries
                    .Select(e => (e.CourtId, e.SlotId, e.Date))
                    .Distinct()
                    .ToList();

                int promoted = 0;
                foreach (var (courtId, slotId, date) in tuples)
                {
                    var b = await waitlist.PromoteAsync(courtId, slotId, date);
                    if (b is not null)
                        promoted++;
                }

                if (promoted > 0)
                    _logger.LogInformation(
                        "Waitlist: promoted {Count} booking(s) to waiters.",
                        promoted);
            }
            finally
            {
                await _lock.ReleaseAsync(LockKey);
            }
        }
    }
}
