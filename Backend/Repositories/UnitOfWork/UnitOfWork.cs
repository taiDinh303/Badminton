using Contract.Repositories.UnitOfWork;
using Badminton.Core.Base;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;
using Npgsql;

namespace Repositories.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly BadmintonBookingDbContext _context;
        private readonly Dictionary<Type, object> _cache = new();
        private bool _disposed;
        public IBookingCommands Bookings { get; }
        public IPaymentCommands Payments { get; }
        public IVoucherCommands Vouchers { get; }

        public UnitOfWork(BadmintonBookingDbContext context)
        {
            _context = context;
            Bookings = new BookingCommands(context);
            Payments = new PaymentCommands(context);
            Vouchers = new VoucherCommands(context);
        }

        public IGenericRepository<T> GetRepository<T>() where T : class
        {
            lock (_cache)
            {
                if (!_cache.TryGetValue(typeof(T), out var repo))
                {
                    repo = new GenericRepository<T>(_context);
                    _cache[typeof(T)] = repo;
                }
                return (IGenericRepository<T>)repo!;
            }
        }

        public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            try { return await _context.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql)
            {
                // Only an allowlisted index identifier crosses the persistence boundary.
                string? constraint = sql.Message.Contains("ux_court_slot_active", StringComparison.Ordinal)
                    || sql.Message.Contains("IX_Bookings_CourtId_Date_SlotId", StringComparison.Ordinal)
                    ? "ux_court_slot_active" : null;
                throw new UniqueConstraintException(constraint, ex);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
            {
                throw new UniqueConstraintException(pg.ConstraintName == "ux_court_slot_active" ? "ux_court_slot_active" : null, ex);
            }
        }

        public async Task RunInTransactionAsync(Func<Task> action,
            CancellationToken ct = default)
        {
            if (_context.Database.CurrentTransaction is not null)
            {
                await action();
                return;
            }
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                await action();
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task LockCourtDateAsync(int courtId, DateOnly date, CancellationToken ct = default)
        {
            if (_context.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Court schedule locks require an active transaction.");
            if (_context.Database.IsNpgsql())
            {
                await _context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '15s'", ct);
                await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1})", [courtId, date.DayNumber], ct);
            }
            else await _context.Database.ExecuteSqlRawAsync(
                "DECLARE @result int; EXEC @result = sp_getapplock @Resource={0}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; " +
                "IF @result < 0 THROW 51000, 'Court schedule busy. Retry.', 1;", [$"court-date:{courtId}:{date:yyyy-MM-dd}"], ct);
        }

        public async Task LockVoucherUserAsync(int userId, CancellationToken ct = default)
        {
            if (_context.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Voucher reservations require an active transaction.");
            if (_context.Database.IsNpgsql())
            {
                await _context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '15s'", ct);
                // Separate namespace from the positive court ID / day-number lock pair.
                await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0}, {1})", [-1, userId], ct);
            }
            else await _context.Database.ExecuteSqlRawAsync(
                "DECLARE @result int; EXEC @result = sp_getapplock @Resource={0}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; " +
                "IF @result < 0 THROW 51000, 'Voucher reservation busy. Retry.', 1;", [$"voucher-user:{userId}"], ct);
        }

        public Task LockPointsUserAsync(int userId, CancellationToken ct = default) =>
            LockResourceAsync($"points-user:{userId}", ct);

        public Task LockBookingRequestAsync(string key, CancellationToken ct = default) =>
            LockResourceAsync($"booking-request:{key}", ct);

        public Task LockTimeSlotsAsync(CancellationToken ct = default) => LockResourceAsync("time-slots", ct);
        public Task LockNotificationAsync(string ledgerKey, CancellationToken ct = default) => LockResourceAsync($"notification:{ledgerKey}", ct);
        public Task LockGroupAsync(int groupId, CancellationToken ct = default) => LockResourceAsync($"group:{groupId}", ct);

        private async Task LockResourceAsync(string resource, CancellationToken ct)
        {
            if (_context.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Business locks require an active transaction.");
            if (_context.Database.IsNpgsql())
            {
                var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(resource));
                var key = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(hash);
                await _context.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '15s'", ct);
                await _context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", [key], ct);
            }
            else await _context.Database.ExecuteSqlRawAsync(
                "DECLARE @result int; EXEC @result = sp_getapplock @Resource={0}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000; " +
                "IF @result < 0 THROW 51000, 'Business operation busy. Retry.', 1;", [resource], ct);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _context.Dispose();
            _disposed = true;
        }
    }
}
