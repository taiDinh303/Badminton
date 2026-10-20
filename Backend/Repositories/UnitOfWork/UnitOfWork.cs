using Contract.Repositories.UnitOfWork;
using Badminton.Core.Base;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly BadmintonBookingDbContext _context;
        private readonly Dictionary<Type, object> _cache = new();
        private bool _disposed;

        public UnitOfWork(BadmintonBookingDbContext context)
        {
            _context = context;
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
        }

        public async Task RunInTransactionAsync(Func<Task> action,
            CancellationToken ct = default)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                await action();
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public Task<int> ExecuteSqlRawAsync(string sql,
            params object[] sqlParameters)
        {
            return _context.Database.ExecuteSqlRawAsync(sql, sqlParameters);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _context.Dispose();
            _disposed = true;
        }
    }
}
