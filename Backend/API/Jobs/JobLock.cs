using System.Collections.Concurrent;
using Badminton.Core.Domain;
using Microsoft.Data.SqlClient;
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
        private SqlConnection? _connection;
        private bool _disposed;

        public JobLock(IConfiguration configuration,
            IServiceProvider? services = null)
        {
            _connectionString =
                configuration.GetConnectionString("BadmintonBooking")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "JobLock requires the BadmintonBooking (or DefaultConnection) " +
                    "connection string to acquire sp_getapplock locks.");
            _metrics = new Lazy<IMetricsReporter?>(
                () => services?.GetService<IMetricsReporter>());
        }

        public async Task<bool> AcquireAsync(string job, int timeoutSeconds = 5,
            CancellationToken ct = default)
        {
            await _gate.WaitAsync(ct);
            try
            {
                var c = await EnsureConnectionAsync(ct);
                using var cmd = c.CreateCommand();
                cmd.CommandText =
                    "SELECT CAST(sp_getapplock(@r, @t, 'Session') AS int)";
                cmd.Parameters.Add(
                    new SqlParameter("@r", $"BadmintonJob_{job}"));
                cmd.Parameters.Add(
                    new SqlParameter("@t", timeoutSeconds * 1000));
                var res = await cmd.ExecuteScalarAsync(ct);
                var code = Convert.ToInt32(
                    res, System.Globalization.CultureInfo.InvariantCulture);

                var ok = code >= 0;
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
                var c = await EnsureConnectionAsync(ct);
                using var cmd = c.CreateCommand();
                cmd.CommandText =
                    "SELECT CAST(sp_releaseapplock(@r, 'Session') AS int)";
                cmd.Parameters.Add(
                    new SqlParameter("@r", $"BadmintonJob_{job}"));
                await cmd.ExecuteScalarAsync(ct);
                _held.TryRemove(job, out _);
            }
            catch
            {
                // Release is best-effort; a lost lock self-heals on next acquire.
            }
            finally { _gate.Release(); }
        }

        private async Task<SqlConnection> EnsureConnectionAsync(CancellationToken ct)
        {
            if (_connection is not null && _connection.State == System.Data.ConnectionState.Open)
                return _connection;
            _connection ??= new SqlConnection(_connectionString);
            await _connection.OpenAsync(ct);
            return _connection;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _connection?.Dispose();
            _connection = null;
        }
    }
}
