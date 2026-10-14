using Contract.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UnitOfWork
{
    /// <summary>
    /// Concrete Unit of Work.
    ///
    /// Encapsulates:
    ///   - a scoped <see cref="BadmintonBookingDbContext"/> (one per request)
    ///   - lazy repository resolution (one per entity type, per UoW)
    ///   - an explicit database transaction boundary
    ///     (<see cref="RunInTransactionAsync"/>) so a flow that spans several
    ///     <c>SaveChangesAsync</c> calls commits all-or-nothing
    ///   - a portable parameterised raw-SQL primitive
    ///     (<see cref="ExecuteSqlRawAsync"/>) for the one atomic counter
    ///     compare-and-set we need (the voucher <c>UsedQty</c> reservation).
    ///
    /// Raw-SQL CAS is provider-neutral (unbracketed identifiers + single-
    /// quoted literals), so the identical statement runs on SQL Server in
    /// production and SQLite in the test host, and joins the ambient
    /// transaction when one is open.
    /// </summary>
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

        public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            _context.SaveChangesAsync(ct);

        /// <summary>
        /// Run <paramref name="action"/> inside one database transaction.
        /// Every <c>SaveChangesAsync</c> / <c>ExecuteSqlRawAsync</c> inside the
        /// action joins this ambient transaction, so a throw rolls them all
        /// back atomically.
        /// </summary>
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

        /// <summary>
        /// Execute a parameterised raw SQL statement. <c>@name</c>
        /// placeholders bind to <paramref name="sqlParameters"/> in order of
        /// appearance. When an ambient transaction is open, the statement
        /// joins it. Returns the affected-row count — the guarded
        /// <c>UPDATE ... WHERE UsedQty &lt; TotalQty</c> reservation relies on
        /// the returned 0/1 to know whether the guard still held.
        /// </summary>
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
