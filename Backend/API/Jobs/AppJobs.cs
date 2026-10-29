using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Notify;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Badminton.API.Jobs
{
    public sealed class PendingBookingExpiryJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<PendingBookingExpiryJob> _logger;
        private readonly JobLock _lock;
        private readonly JobHeartbeatRegistry _heartbeat;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private const string LockKey = nameof(PendingBookingExpiryJob);

        public PendingBookingExpiryJob(IServiceScopeFactory scopes,
            ILogger<PendingBookingExpiryJob> logger,
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
                try { await RunOnceAsync(); }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex,
                        "{Job} tick failed; will retry on the next interval.",
                        nameof(PendingBookingExpiryJob));
                }
                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync()
        {
            // Multi-instance guard: only one app instance executes this tick.
            if (!await _lock.AcquireAsync(LockKey))
            {
                _logger.LogDebug("{Job}: another instance owns this tick; skipping.",
                    nameof(PendingBookingExpiryJob));
                return;
            }
            try
            {
                _heartbeat.Record(LockKey);
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

                var expiry = sp.GetRequiredService<Services.BookingExpiryService>();
                var applied = new List<Booking>();
                foreach (var booking in expired)
                    if (await expiry.ExpireAsync(booking.Id, now)) applied.Add(booking);
                _logger.LogInformation("Expired {Count} pending booking(s), released vouchers + restored points.", applied.Count);

                var notify = sp.GetRequiredService<INotifyService>();
                var waitlist = sp.GetRequiredService<Contract.Services.Waitlist.IWaitlistService>();
                foreach (var b in applied)
                {
                    try
                    {
                        await notify.NotifyBookingExpiredAsync(b.Id);
                        // Slot just freed up — promote the first waiter if any.
                        await waitlist.PromoteAsync(b.CourtId, b.SlotId, b.Date);
                    }
                    catch (Exception ex)
                    {
                        // A notify/waitlist failure must not re-block the tick.
                        _logger.LogError(ex,
                            "Expiry side-effects failed for booking {Id}; tick continues.", b.Id);
                    }
                }
            }
            finally
            {
                await _lock.ReleaseAsync(LockKey);
            }
        }
    }

    public sealed class AutoCompleteJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AutoCompleteJob> _logger;
        private readonly JobLock _lock;
        private readonly JobHeartbeatRegistry _heartbeat;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private const string LockKey = nameof(AutoCompleteJob);

        public AutoCompleteJob(IServiceScopeFactory scopes,
            ILogger<AutoCompleteJob> logger,
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
                try { await RunOnceAsync(); }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex,
                        "{Job} tick failed; will retry on the next interval.",
                        nameof(AutoCompleteJob));
                }
                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync()
        {
            if (!await _lock.AcquireAsync(LockKey))
            {
                _logger.LogDebug("{Job}: another instance owns this tick; skipping.",
                    nameof(AutoCompleteJob));
                return;
            }
            try
            {
                _heartbeat.Record(LockKey);
                using var scope = _scopes.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var bookingRepo = uow.GetRepository<Booking>();
                var completion = scope.ServiceProvider.GetRequiredService<Services.BookingCompletionService>();
                await scope.ServiceProvider.GetRequiredService<Contract.Services.Report.IInventoryHistoryService>().CaptureTodayAsync();
                var bookings = await bookingRepo.GetByConditionAsync(b => b.Status == BookingStatus.Confirmed);
                var slotIds = bookings.Select(b => b.SlotId).Distinct().ToList();
                var slots = (await uow.GetRepository<TimeSlot>().GetByConditionAsync(s => slotIds.Contains(s.Id)))
                    .ToDictionary(s => s.Id);
                var now = HanoiTime.HanoiNow();
                int completed = 0;
                foreach (var booking in bookings.Where(b => slots.TryGetValue(b.SlotId, out var slot)
                    && HanoiTime.SlotEnd(b.Date, slot.EndTime) <= now))
                    if (await completion.CompleteAsync(booking.Id, onlyIfDue: true) is not null) completed++;

                if (completed > 0)
                {
                    await uow.SaveChangesAsync();
                    _logger.LogInformation(
                        "Auto-completed {Count} confirmed booking(s) past slot end; points banked.",
                        completed);
                }
            }
            finally
            {
                await _lock.ReleaseAsync(LockKey);
            }
        }
    }

    public sealed class VoucherExpiryJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<VoucherExpiryJob> _logger;
        private readonly JobLock _lock;
        private readonly JobHeartbeatRegistry _heartbeat;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        private const string LockKey = nameof(VoucherExpiryJob);

        public VoucherExpiryJob(IServiceScopeFactory scopes,
            ILogger<VoucherExpiryJob> logger,
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
                try { await RunOnceAsync(); }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex,
                        "{Job} tick failed; will retry on the next interval.",
                        nameof(VoucherExpiryJob));
                }
                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync()
        {
            if (!await _lock.AcquireAsync(LockKey))
            {
                _logger.LogDebug("{Job}: another instance owns this tick; skipping.",
                    nameof(VoucherExpiryJob));
                return;
            }
            try
            {
                _heartbeat.Record(LockKey);
                using var scope = _scopes.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var today = HanoiTime.HanoiToday();
                var repo = uow.GetRepository<Voucher>();
                var expired = await repo.GetByConditionAsync(v =>
                    v.IsActive && v.ValidUntil.HasValue && v.ValidUntil.Value < today);
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
            finally
            {
                await _lock.ReleaseAsync(LockKey);
            }
        }
    }

    public sealed class NoShowResetJob : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<NoShowResetJob> _logger;
        private readonly JobLock _lock;
        private readonly JobHeartbeatRegistry _heartbeat;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        private const string LockKey = nameof(NoShowResetJob);

        public NoShowResetJob(IServiceScopeFactory scopes,
            ILogger<NoShowResetJob> logger,
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
                try { await RunOnceAsync(); }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex,
                        "{Job} tick failed; will retry on the next interval.",
                        nameof(NoShowResetJob));
                }
                try { await Task.Delay(Interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task RunOnceAsync()
        {
            if (!await _lock.AcquireAsync(LockKey))
            {
                _logger.LogDebug("{Job}: another instance owns this tick; skipping.",
                    nameof(NoShowResetJob));
                return;
            }
            try
            {
                _heartbeat.Record(LockKey);
                using var scope = _scopes.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var currentMonthStart = HanoiTime.HanoiToday().MonthStart();

                // All users currently blocked whose blocked-month has passed.
                var users = await uow.GetRepository<User>().GetByConditionAsync(u =>
                    u.NoShowLimited && u.NoShowLimitedPeriod != null);

                int reset = 0;
                foreach (var candidate in users)
                {
                    await uow.RunInTransactionAsync(async () =>
                    {
                        await uow.LockPointsUserAsync(candidate.Id);
                        var u = (await uow.GetRepository<User>().GetByConditionAsync(x => x.Id == candidate.Id)).SingleOrDefault();
                        if (u is null || !u.NoShowLimited || u.NoShowLimitedPeriod is not { } period || currentMonthStart <= period)
                            return;
                        u.NoShowLimited = false;
                        u.NoShowLimitedPeriod = null;
                        u.UpdatedAt = DateTime.UtcNow;
                        uow.GetRepository<User>().UpdateProperties(u, nameof(User.NoShowLimited), nameof(User.NoShowLimitedPeriod), nameof(User.UpdatedAt));
                        await uow.SaveChangesAsync();
                        reset++;
                    });
                }

                if (reset > 0)
                {
                    _logger.LogInformation("Unblocked {Count} user(s) whose no-show limit period ended.", reset);
                }
            }
            finally
            {
                await _lock.ReleaseAsync(LockKey);
            }
        }
    }
}
