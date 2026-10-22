using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Npgsql;
using NpgsqlTypes;

// Explicit one-off copy into an EMPTY, already migrated PostgreSQL database.
// Never invoked by application startup. Credentials are read from ignored files.
if (args.Length is 5 or 6 && args[0] == "--connect")
{
    try
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = args[2], Username = args[3], Database = "postgres", Port = 5432,
            Password = (await File.ReadAllTextAsync(args[1])).Trim(), SslMode = SslMode.VerifyFull,
            Timeout = 10, MaxPoolSize = 10, IncludeErrorDetail = false
        };
        if (args.Length == 6) builder.RootCertificate = Path.GetFullPath(args[5]);
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await File.WriteAllTextAsync(args[4], builder.ConnectionString);
        Console.WriteLine("CONNECTION_VERIFIED: TLS VerifyFull; credentials saved to ignored output file.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("CONNECTION_FAILED: " + ex.GetType().Name
            + (ex is PostgresException pg ? " SQLSTATE=" + pg.SqlState : "")
            + "; inner=" + ex.InnerException?.GetType().Name);
        return 1;
    }
}
if (args.Length != 3 || args[2] != "--execute")
{
    Console.Error.WriteLine("Usage: DatabaseTransfer <source-connection-file> <target-connection-file> --execute");
    return 2;
}
try
{
    await Transfer(args[0], args[1]);
    return 0;
}
catch (Exception ex)
{
    // Provider messages/row values can contain credentials or personal data.
    Console.Error.WriteLine("TRANSFER_FAILED: " + ex.GetType().Name
        + (ex is PostgresException pg ? " SQLSTATE=" + pg.SqlState : "")
        + "; inner=" + ex.InnerException?.GetType().Name
        + "; transaction rolled back; no connection strings or row values logged.");
    return 1;
}

static async Task Transfer(string sourceFile, string targetFile)
{
    string[] tables = ["Branches", "TimeSlots", "Vouchers", "Users", "Courts", "PlayGroups", "Bookings", "AuditLogs", "BookingPlayers", "CourtDateStatus", "GroupMembers", "Notifications", "Payments", "PointLedger", "Reviews", "VoucherRedemptions", "WaitlistEntries"];
    await using var source = new SqlConnection((await File.ReadAllTextAsync(sourceFile)).Trim());
    var targetSettings = new NpgsqlConnectionStringBuilder((await File.ReadAllTextAsync(targetFile)).Trim()) { CommandTimeout = 120 };
    await using var target = new NpgsqlConnection(targetSettings.ConnectionString);
    await source.OpenAsync();
    await target.OpenAsync();
    await using var readTx = (SqlTransaction)await source.BeginTransactionAsync(IsolationLevel.Serializable);
    await using var writeTx = await target.BeginTransactionAsync();
    // Lock all destinations together before checking emptiness; no partial overwrite.
    await using (var guard = new NpgsqlCommand("LOCK TABLE " + string.Join(",", tables.Select(Quote)) + " IN ACCESS EXCLUSIVE MODE", target, writeTx))
        await guard.ExecuteNonQueryAsync();
    foreach (var table in tables)
    {
        await using var count = new NpgsqlCommand($"SELECT count(*) FROM {Quote(table)}", target, writeTx);
        if (Convert.ToInt64(await count.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("Destination must be empty.");
    }
    foreach (var table in tables)
    {
        Console.WriteLine("COPYING " + table);
        var columns = new List<(string Name, string Type)>();
        await using (var schema = new NpgsqlCommand("SELECT column_name, data_type FROM information_schema.columns WHERE table_schema='public' AND table_name=@table ORDER BY ordinal_position", target, writeTx))
        {
            schema.Parameters.AddWithValue("table", table);
            await using var reader = await schema.ExecuteReaderAsync();
            while (await reader.ReadAsync()) columns.Add((reader.GetString(0), reader.GetString(1)));
        }
        if (columns.Count == 0) throw new InvalidOperationException("Missing migrated table.");
        var names = string.Join(",", columns.Select(c => Quote(c.Name)));
        var order = table switch { "BookingPlayers" => "\"BookingId\",\"UserId\"", "GroupMembers" => "\"GroupId\",\"UserId\"", "CourtDateStatus" => "\"CourtId\",\"Date\"", _ => "\"Id\"" };
        var rows = new List<object[]>();
        await using (var read = new SqlCommand($"SELECT {names} FROM {Quote(table)} ORDER BY {order}", source, readTx))
        {
            await using var reader = await read.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new object[columns.Count];
                for (int i = 0; i < row.Length; i++) row[i] = Normalize(reader.GetValue(i), columns[i].Type);
                rows.Add(row);
            }
        }
        await using (var insert = await target.BeginBinaryImportAsync($"COPY {Quote(table)} ({names}) FROM STDIN (FORMAT BINARY)"))
        {
            foreach (var row in rows)
            {
                await insert.StartRowAsync();
                for (int i = 0; i < row.Length; i++)
                    if (row[i] is DBNull) await insert.WriteNullAsync();
                    else await insert.WriteAsync(row[i], PgType(columns[i].Type));
            }
            await insert.CompleteAsync();
        }
        // Verify all values and IDs, including nulls, using the same primary key ordering.
        await using (var verify = new NpgsqlCommand($"SELECT {names} FROM {Quote(table)} ORDER BY {order}", target, writeTx))
        {
            await using var reader = await verify.ExecuteReaderAsync();
            int rowIndex = 0;
            while (await reader.ReadAsync())
            {
                if (rowIndex >= rows.Count) throw new InvalidOperationException("Row count differs.");
                for (int i = 0; i < columns.Count; i++)
                    if (Canonical(rows[rowIndex][i]) != Canonical(Normalize(reader.GetValue(i), columns[i].Type)))
                        throw new InvalidOperationException("Value verification failed.");
                rowIndex++;
            }
            if (rowIndex != rows.Count) throw new InvalidOperationException("Row count differs.");
        }
        if (columns.Any(c => c.Name == "Id"))
        {
            await using var reset = new NpgsqlCommand($"SELECT setval(pg_get_serial_sequence(@table, 'Id'), GREATEST(COALESCE(MAX(\"Id\"), 0),1), COUNT(*) > 0) FROM {Quote(table)}", target, writeTx);
            reset.Parameters.AddWithValue("table", "public." + Quote(table));
            await reset.ExecuteNonQueryAsync();
        }
        Console.WriteLine($"VERIFIED {table}: {rows.Count} rows (values/IDs preserved)");
    }
    await writeTx.CommitAsync();
    await readTx.CommitAsync();
    Console.WriteLine("TRANSFER_COMMITTED: 17 tables; source read-only; target verified before commit.");
}

static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
static object Normalize(object value, string type) => value is DBNull ? DBNull.Value : type switch
{
    "date" => value is DateTime d ? DateOnly.FromDateTime(d) : value,
    "time without time zone" => value is TimeSpan t ? TimeOnly.FromTimeSpan(t) : value,
    "timestamp with time zone" when value is DateTime d => new DateTime(d.Ticks - d.Ticks % 10, DateTimeKind.Utc),
    _ => value
};
static string Canonical(object value) => value switch
{
    DBNull => "<null>",
    decimal d => d.ToString("G29", CultureInfo.InvariantCulture),
    DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
    _ => value.ToString()!
};
static NpgsqlDbType PgType(string type) => type switch
{
    "integer" => NpgsqlDbType.Integer,
    "bigint" => NpgsqlDbType.Bigint,
    "boolean" => NpgsqlDbType.Boolean,
    "numeric" => NpgsqlDbType.Numeric,
    "double precision" => NpgsqlDbType.Double,
    "real" => NpgsqlDbType.Real,
    "date" => NpgsqlDbType.Date,
    "time without time zone" => NpgsqlDbType.Time,
    "interval" => NpgsqlDbType.Interval,
    "timestamp with time zone" => NpgsqlDbType.TimestampTz,
    "text" or "character varying" => NpgsqlDbType.Text,
    _ => throw new InvalidOperationException("Unsupported column type.")
};
