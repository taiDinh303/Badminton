# Local operations acceptance

Run from the repository root. This executable only permits generated `BadmintonTest_<8hex>` databases on loopback servers; it never accepts an application/production database. Child processes inherit only the fixture connection for their work. All source and restore fixture databases are disposed after the rehearsal.

Windows SQL Server LocalDB:

```powershell
$env:BADMINTON_TEST_SQLSERVER='(localdb)\MSSQLLocalDB'
Remove-Item Env:BADMINTON_TEST_POSTGRES -ErrorAction SilentlyContinue
dotnet run --project tools/OperationsRehearsal/OperationsRehearsal.csproj -c Release
```

PostgreSQL (start an isolated local server first; supply a matching or newer PostgreSQL client tools directory):

```powershell
$env:BADMINTON_TEST_POSTGRES='Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Password=<local-test-password>'
dotnet run --project tools/OperationsRehearsal/OperationsRehearsal.csproj -c Release -- C:/path/to/postgresql/bin
```

The rehearsal runs eight completion processes and eight expiry processes against the same booking transitions, checks single ledger/voucher/outbox effects, kills a process holding the real database job lock, and verifies another process can acquire it. It then performs native SQL Server BACKUP/RESTORE or PostgreSQL pg_dump/pg_restore into a second generated fixture. It compares all mapped rows, migration history and indexes, checks pending migrations, and exercises the restored active-slot uniqueness constraint. Only the two known equivalent PostgreSQL enum-array cast representations are normalized in index DDL comparisons.

Evidence and synthetic backups are written under ignored `artifacts/operations-acceptance`. This demonstrates bounded process concurrency and local restore correctness; it is not production endurance, a measured RPO/RTO, or a managed Supabase project restore.
