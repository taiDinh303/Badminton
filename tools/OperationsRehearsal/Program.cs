using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Badminton.API.Jobs;
using Badminton.Core.Base;
using Badminton.Core.Security;
using Badminton.Tests;
using Contract.Repositories.UnitOfWork;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Repositories.Context;
using Repositories.UnitOfWork;
using Services;
using Services.DI;

// Test-only executable; never included in the API image. All connections and
// restore/drop targets are verified loopback BadmintonTest_* fixtures.
if (args.FirstOrDefault() == "--worker")
{
    var connection = Environment.GetEnvironmentVariable("BADMINTON_PROBE_CONNECTION") ?? throw new InvalidOperationException("Missing fixture.");
    var pg = Environment.GetEnvironmentVariable("BADMINTON_TEST_POSTGRES") is not null;
    Guard(connection, pg);
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
        ["ConnectionStrings:BadmintonBooking"] = connection, ["Database:Provider"] = pg ? "PostgreSQL" : "SqlServer", ["Payment:Provider"] = "mock"
    }).Build();
    if (args[1] == "lock")
    {
        using var jobLock = new JobLock(config);
        if (!await jobLock.AcquireAsync("operations-crash-rehearsal", 0)) throw new InvalidOperationException("Lock unavailable.");
        Console.WriteLine("LOCK_HELD");
        if (args.Length > 2) await Task.Delay(Timeout.Infinite);
        return;
    }
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton<ICurrentUser>(new TestHttpContextAccessor());
    services.AddBookingServices(config);
    services.AddScoped<IUnitOfWork, UnitOfWork>();
    if (pg)
    {
        services.AddDbContext<PostgresBookingDbContext>(o => o.UseNpgsql(connection));
        services.AddScoped<BadmintonBookingDbContext>(sp => sp.GetRequiredService<PostgresBookingDbContext>());
    }
    else services.AddDbContext<BadmintonBookingDbContext>(o => o.UseSqlServer(connection));
    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    var id = int.Parse(args[2], CultureInfo.InvariantCulture);
    try
    {
        if (args[1] == "complete") {
            var db = scope.ServiceProvider.GetRequiredService<BadmintonBookingDbContext>();
            var booking = await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == id);
            var slot = await db.TimeSlots.AsNoTracking().SingleAsync(s => s.Id == booking.SlotId);
            Badminton.Core.Utils.HanoiTime.NowUtc = () => DateTime.SpecifyKind(Badminton.Core.Utils.HanoiTime.SlotEnd(booking.Date, slot.EndTime).AddHours(-7).AddSeconds(1), DateTimeKind.Utc);
            await scope.ServiceProvider.GetRequiredService<BookingCompletionService>().CompleteAsync(id);
        }
        else if (args[1] == "expire") await scope.ServiceProvider.GetRequiredService<BookingExpiryService>().ExpireAsync(id, DateTime.UtcNow);
        else throw new InvalidOperationException("Unknown worker.");
    }
    catch (ConflictException) { /* Another process already performed this transition. */ }
    return;
}

var isPg = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BADMINTON_TEST_POSTGRES"));
await using var fixture = new TestHost(useMigrations: true);
Guard(fixture.ConnectionString, isPg);
var directory = Path.GetFullPath(Path.Combine("artifacts", "operations-acceptance", (isPg ? "postgres-" : "sqlserver-") + Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(directory);
var completed = await fixture.MakeConfirmedBookingAsync(fixture.Alice, fixture.Court1, fixture.SlotNoon);
await fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(b => b.Id == completed.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.CheckedInAt, DateTime.UtcNow)));
await RunWorkers("complete", completed.Id, 8);
Check(await fixture.CountPointRowsAsync(completed.Id) == 1, "Eight completion processes must create exactly one point ledger entry.");
await fixture.CountPointsIntoAsync(fixture.Bob, 1000, "operations rehearsal fixture");
await fixture.AddVoucherAsync("OPS", 10, 1, 10000);
var pending = await fixture.BookAsync(fixture.Bob, fixture.Court2, fixture.SlotNoon, fixture.FutureDate.ToString("yyyy-MM-dd"), 1000, "OPS");
await fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(b => b.Id == pending.Id)
    .ExecuteUpdateAsync(s => s.SetProperty(b => b.PendingExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
await RunWorkers("expire", pending.Id, 8);
Check((await fixture.GetVoucherAsync("OPS")).UsedQty == 0, "Expiry must release voucher once.");
await fixture.RunScopeAsync(async sp =>
{
    var db = sp.GetRequiredService<BadmintonBookingDbContext>();
    Check(await db.PointLedger.CountAsync(p => p.LedgerKey == "restore:" + pending.Id) == 1, "Expiry must restore points once.");
    Check(await db.Notifications.CountAsync(n => n.BookingId == pending.Id) == 1, "Expiry notification must survive in the outbox once.");
});
using (var holder = Worker("lock", "hold"))
{
    try
    {
        var line = await holder.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(45));
        Check(line == "LOCK_HELD", "Crash holder failed to acquire lock.");
    }
    finally { if (!holder.HasExited) holder.Kill(entireProcessTree: true); await holder.WaitForExitAsync(); }
}
using (var recovered = Worker("lock"))
{
    await recovered.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
    Check(recovered.ExitCode == 0, "A new process must recover the job lock after process death.");
}
Console.WriteLine("PROCESS_REHEARSAL_OK: completion=8 expiry=8 crash_lock=1 recovery=1");

var expected = await fixture.RunScopeAsync(async sp => await Snapshot(sp.GetRequiredService<BadmintonBookingDbContext>()));
var restoredName = fixture.DbName + "_restore";
var restoredConnection = isPg ? new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = restoredName }.ConnectionString
    : new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = restoredName }.ConnectionString;
Guard(restoredConnection, isPg);
try
{
    if (isPg)
    {
        var pgTools = args.FirstOrDefault() ?? throw new InvalidOperationException("Pass the PostgreSQL tools bin directory.");
        var pg = new NpgsqlConnectionStringBuilder(fixture.ConnectionString);
        var backup = Path.Combine(directory, "fixture.dump");
        await Exec(Path.Combine(pgTools, OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump"), ["--format=custom", "--no-owner", "--no-privileges", "--host=127.0.0.1", "--port=" + pg.Port, "--username=" + pg.Username, "--dbname=" + fixture.DbName, "--file=" + backup], pg.Password);
        await using var admin = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = "postgres" }.ConnectionString);
        await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE \"{restoredName}\"", admin).ExecuteNonQueryAsync();
        await Exec(Path.Combine(pgTools, OperatingSystem.IsWindows() ? "pg_restore.exe" : "pg_restore"), ["--exit-on-error", "--no-owner", "--no-privileges", "--host=127.0.0.1", "--port=" + pg.Port, "--username=" + pg.Username, "--dbname=" + restoredName, backup], pg.Password);
    }
    else
    {
        var backup = Path.Combine(directory, "fixture.bak");
        await using var master = new SqlConnection(new SqlConnectionStringBuilder(fixture.ConnectionString) { InitialCatalog = "master" }.ConnectionString);
        await master.OpenAsync();
        await using (var cmd = new SqlCommand($"BACKUP DATABASE [{fixture.DbName}] TO DISK=@path WITH CHECKSUM, INIT", master))
        { cmd.Parameters.AddWithValue("@path", backup); await cmd.ExecuteNonQueryAsync(); }
        var files = new List<(string Name, string Type)>();
        await using (var cmd = new SqlCommand("RESTORE FILELISTONLY FROM DISK=@path", master))
        {
            cmd.Parameters.AddWithValue("@path", backup);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) files.Add((reader.GetString(0), reader.GetString(2)));
        }
        var moves = files.Select((f, i) => $"MOVE N'{f.Name.Replace("'", "''")}' TO N'{Path.Combine(directory, "restore" + i + (f.Type == "L" ? ".ldf" : ".mdf")).Replace("'", "''")}'");
        await using var restore = new SqlCommand($"RESTORE DATABASE [{restoredName}] FROM DISK=@path WITH CHECKSUM, {string.Join(", ", moves)}", master) { CommandTimeout = 120 };
        restore.Parameters.AddWithValue("@path", backup);
        await restore.ExecuteNonQueryAsync();
    }
    await using BadmintonBookingDbContext restored = isPg
        ? new PostgresBookingDbContext(new DbContextOptionsBuilder<PostgresBookingDbContext>().UseNpgsql(restoredConnection).Options)
        : new BadmintonBookingDbContext(new DbContextOptionsBuilder<BadmintonBookingDbContext>().UseSqlServer(restoredConnection).Options);
    var actual = await Snapshot(restored);
    await File.WriteAllTextAsync(Path.Combine(directory, "before.txt"), expected);
    await File.WriteAllTextAsync(Path.Combine(directory, "restored.txt"), actual);
    Check(expected == actual, "Restored rows, migration history and indexes differ from the original fixture.");
    Check(!(await restored.Database.GetPendingMigrationsAsync()).Any(), "Restored fixture has pending migrations.");
    // Exercise the restored booking constraint too, rather than relying only
    // on a textual representation of its partial-index predicate.
    await using (var tx = await restored.Database.BeginTransactionAsync())
    {
        var original = await restored.Bookings.FirstAsync(b => b.Status == Badminton.Core.Enums.BookingStatus.Completed);
        await restored.Bookings.Where(b => b.Id == original.Id).ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, Badminton.Core.Enums.BookingStatus.Confirmed));
        restored.Bookings.Add(new Contract.Repositories.Entity.Booking { BookingCode = "RESTORE-UNIQUE", UserId = original.UserId,
            CourtId = original.CourtId, BranchId = original.BranchId, SlotId = original.SlotId, Date = original.Date, PlayerCount = 1,
            Status = Badminton.Core.Enums.BookingStatus.Pending, TotalAmount = original.TotalAmount, PayableAmount = original.PayableAmount,
            PendingExpiresAt = DateTime.UtcNow.AddMinutes(10) });
        var rejected = false;
        try { await restored.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "ux_court_slot_active" }
            || ex.InnerException is SqlException { Number: 2601 or 2627 }) { rejected = true; }
        Check(rejected, "Restored unique active-slot constraint did not reject a conflicting booking.");
        await tx.RollbackAsync();
    }
    var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
    Console.WriteLine("BACKUP_RESTORE_OK: all mapped tables + migration history + indexes; checksum=" + checksum);
    await File.WriteAllTextAsync(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { provider = isPg ? "PostgreSQL" : "SqlServer", fixture = fixture.DbName, snapshot = checksum, workerProcesses = 18, backupRestored = true }));
}
finally
{
    // The only drop target is derived from this execution's generated fixture.
    Guard(restoredConnection, isPg);
    if (isPg)
    {
        await using var db = new PostgresBookingDbContext(new DbContextOptionsBuilder<PostgresBookingDbContext>().UseNpgsql(restoredConnection).Options);
        await db.Database.EnsureDeletedAsync();
    }
    else
    {
        await using var db = new BadmintonBookingDbContext(new DbContextOptionsBuilder<BadmintonBookingDbContext>().UseSqlServer(restoredConnection).Options);
        await db.Database.EnsureDeletedAsync();
    }
}

async Task RunWorkers(string mode, int id, int count)
{
    var children = Enumerable.Range(0, count).Select(_ => Worker(mode, id.ToString(CultureInfo.InvariantCulture))).ToArray();
    try
    {
        await Task.WhenAll(children.Select(async p => {
            var error = p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
            if (p.ExitCode != 0) throw new InvalidOperationException("Worker failed: " + await error);
        }));
    }
    finally { foreach (var p in children) { if (!p.HasExited) p.Kill(entireProcessTree: true); p.Dispose(); } }
}
Process Worker(params string[] workerArgs)
{
    var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add(typeof(Program).Assembly.Location); start.ArgumentList.Add("--worker");
    foreach (var arg in workerArgs) start.ArgumentList.Add(arg);
    start.Environment["BADMINTON_PROBE_CONNECTION"] = fixture.ConnectionString;
    return Process.Start(start)!;
}
static void Guard(string connection, bool pg)
{
    var name = pg ? new NpgsqlConnectionStringBuilder(connection).Database : new SqlConnectionStringBuilder(connection).InitialCatalog;
    var host = pg ? new NpgsqlConnectionStringBuilder(connection).Host : new SqlConnectionStringBuilder(connection).DataSource;
    if (name is null || !System.Text.RegularExpressions.Regex.IsMatch(name, "^BadmintonTest_[a-f0-9]{8}(_restore)?$")
        || (pg ? host is not ("localhost" or "127.0.0.1") : host is not ("(localdb)\\MSSQLLocalDB" or "." or "localhost" or "127.0.0.1" or ".\\SQLEXPRESS")))
        throw new InvalidOperationException("Operations rehearsal requires a generated loopback BadmintonTest_* fixture.");
}
static async Task Exec(string executable, string[] arguments, string? password)
{
    var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var arg in arguments) start.ArgumentList.Add(arg);
    start.Environment["PGPASSWORD"] = password;
    using var child = Process.Start(start)!;
    var stderr = child.StandardError.ReadToEndAsync();
    await child.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
    if (child.ExitCode != 0) throw new InvalidOperationException("Backup/restore tool failed: " + await stderr);
}
static void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
static async Task<string> Snapshot(BadmintonBookingDbContext db)
{
    var sb = new StringBuilder();
    await db.Database.OpenConnectionAsync();
    var pg = db.Database.IsNpgsql();
    foreach (var table in db.Model.GetEntityTypes().Select(t => t.GetTableName()!).Append("__EFMigrationsHistory").Distinct().Order())
    {
        sb.AppendLine(table);
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT * FROM " + (pg ? $"\"{table}\"" : $"[{table}]") + " ORDER BY 1";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            for (var i = 0; i < reader.FieldCount; i++) sb.Append(JsonSerializer.Serialize(reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture))).Append('|');
    }
    await using (var indexes = db.Database.GetDbConnection().CreateCommand())
    {
        indexes.CommandText = pg ? "SELECT tablename,indexname,indexdef FROM pg_indexes WHERE schemaname='public' ORDER BY tablename,indexname"
            : "SELECT t.name,i.name,i.is_unique FROM sys.indexes i JOIN sys.tables t ON t.object_id=i.object_id ORDER BY t.name,i.name";
        await using var reader = await indexes.ExecuteReaderAsync();
        while (await reader.ReadAsync()) for (var i = 0; i < reader.FieldCount; i++)
        {
            var value = Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "";
            // pg_dump can move the enum-array cast onto its elements. These
            // two exact forms are equivalent; preserve every other DDL byte.
            if (pg) value = value.Replace("(ARRAY['Pending'::character varying, 'Confirmed'::character varying])::text[]",
                "ARRAY[('Pending'::character varying)::text, ('Confirmed'::character varying)::text]", StringComparison.Ordinal);
            sb.Append(value).Append('|');
        }
    }
    if (pg)
    {
        await using var privacy = db.Database.GetDbConnection().CreateCommand();
        privacy.CommandText = "SELECT relname,relrowsecurity FROM pg_class WHERE relnamespace='public'::regnamespace AND relkind='r' ORDER BY relname";
        await using var reader = await privacy.ExecuteReaderAsync();
        while (await reader.ReadAsync()) sb.Append(reader.GetString(0)).Append('|').Append(reader.GetBoolean(1)).Append('|');
    }
    return sb.ToString();
}
