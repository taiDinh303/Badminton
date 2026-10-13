using Contract.Repositories.UnitOfWork;
using Repositories.Context;

namespace Repositories.UnitOfWork
{
    /// <summary>
    /// UNIT OF WORK: owns the transactional boundary. Each scoped request gets
    /// ONE UnitOfWork; all repositories resolve against the same context, so a
    /// single <c>SaveChangesAsync()</c> commits them atomically. This is
    /// encapsulation: the "all-or-nothing" rule lives in exactly one class.
    /// </summary>
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly BadmintonBookingDbContext _context;
        private bool _disposed;
        private readonly Dictionary<Type, object> _cache = new();

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

        public void Dispose()
        {
            if (_disposed) return;
            _context.Dispose();
            _disposed = true;
        }
    }
}
