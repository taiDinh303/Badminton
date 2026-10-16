using Contract.Repositories.UnitOfWork;
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

        public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
            _context.SaveChangesAsync(ct);

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
