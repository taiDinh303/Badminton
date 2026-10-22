using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Repositories;

// Dedicated session: advisory locks must never run on a transaction pooler.
public sealed class JobLease(string connectionString, bool postgres) : IDisposable
{
    private DbConnection? _connection;

    public async Task<bool> AcquireAsync(string name, int timeoutSeconds, CancellationToken ct)
    {
        if (_connection is null || _connection.State != ConnectionState.Open)
        {
            _connection?.Dispose();
            _connection = postgres ? new NpgsqlConnection(connectionString) : new SqlConnection(connectionString);
            await _connection.OpenAsync(ct);
        }
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = postgres
            ? "SELECT pg_try_advisory_lock(hashtextextended(@r, 0))"
            : "DECLARE @result int; EXEC @result = sp_getapplock @Resource=@r, @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=@t; SELECT @result;";
        Add(cmd, "r", "BadmintonJob_" + name);
        if (!postgres) Add(cmd, "t", timeoutSeconds * 1000);
        var result = await cmd.ExecuteScalarAsync(ct);
        return postgres ? result is true : Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture) >= 0;
    }

    public async Task ReleaseAsync(string name, CancellationToken ct)
    {
        // Never reopen a lost session to release a lock owned by another session.
        if (_connection?.State != ConnectionState.Open) return;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = postgres
            ? "SELECT pg_advisory_unlock(hashtextextended(@r, 0))"
            : "DECLARE @result int; EXEC @result = sp_releaseapplock @Resource=@r, @LockOwner='Session'; SELECT @result;";
        Add(cmd, "r", "BadmintonJob_" + name);
        await cmd.ExecuteScalarAsync(ct);
    }

    private static void Add(DbCommand cmd, string name, object value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        cmd.Parameters.Add(parameter);
    }

    public void Dispose() => _connection?.Dispose();
}
