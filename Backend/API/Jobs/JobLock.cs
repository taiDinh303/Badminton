using System.Collections.Concurrent;
using Badminton.Core.Domain;
using Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Badminton.API.Jobs
{
    public sealed class JobLock : IDisposable
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly ConcurrentDictionary<string, byte> _held = new();
        private readonly Lazy<IMetricsReporter?> _metrics;
        private readonly JobLease _lease;
        private bool _disposed;

        public JobLock(IConfiguration configuration,
            IServiceProvider? services = null)
        {
            _connectionString =
                configuration.GetConnectionString("BadmintonBooking")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "JobLock requires the BadmintonBooking (or DefaultConnection) " +
                    "connection string to acquire database job locks.");
            _lease = new JobLease(_connectionString, string.Equals(configuration["Database:Provider"], "PostgreSQL", StringComparison.OrdinalIgnoreCase));
            _metrics = new Lazy<IMetricsReporter?>(
                () => services?.GetService<IMetricsReporter>());
        }

        public async Task<bool> AcquireAsync(string job, int timeoutSeconds = 5,
            CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                if (_held.ContainsKey(job)) return false;
                var ok = await _lease.AcquireAsync(job, timeoutSeconds, ct);
                if (ok)
                {
                    _held[job] = 1;
                    try { _metrics.Value?.CountJobLockAcquired(job); }
                    catch { /* metrics must never break the job */ }
                }
                return ok;
            }
            finally { _gate.Release(); }
        }

        public async Task ReleaseAsync(string job, CancellationToken ct = default)
        {
            if (!_held.ContainsKey(job)) return;
            await _gate.WaitAsync(ct);
            try
            {
                await _lease.ReleaseAsync(job, ct);
                _held.TryRemove(job, out _);
            }
            catch
            {
                // Release is best-effort; a lost lock self-heals on next acquire.
            }
            finally { _gate.Release(); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lease.Dispose();
        }
    }
}
