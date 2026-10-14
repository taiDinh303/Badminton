using System.Linq.Expressions;
using Contract.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UnitOfWork
{
    /// <summary>
    /// Concrete generic repository (ENCAPSULATION: hides EF Core's <c>DbSet</c>
    /// behind <c>IGenericRepository&lt;T&gt;</c>). Services only ever see the
    /// contract, never the EF API.
    /// </summary>
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        private readonly BadmintonBookingDbContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(BadmintonBookingDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            // FindAsync works with any PK type; our PKs are all int.
            return await _dbSet.FindAsync([id], ct);
        }

        public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
            await _dbSet.AsNoTracking().ToListAsync(ct);

        public async Task<IReadOnlyList<T>> GetByConditionAsync(
            Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            await _dbSet.AsNoTracking().Where(predicate).ToListAsync(ct);

        public Task<int> CountByConditionAsync(
            Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            _dbSet.AsNoTracking().Where(predicate).CountAsync(ct);

        /// <summary>
        /// Anti-N+1 paged read: single round trip for both the window and the
        /// total. <see cref="OrderDirection.Desc"/> is the usual "newest
        /// first" for list endpoints; <c>Asc</c> is for ordered scans.
        /// </summary>
        public async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, DateTime>> orderBy,
            OrderDirection direction,
            Page page,
            CancellationToken ct = default)
        {
            var baseQ = _dbSet.AsNoTracking().Where(predicate);
            IOrderedQueryable<T> orderedQ = direction == OrderDirection.Desc
                ? baseQ.OrderByDescending(orderBy)
                : baseQ.OrderBy(orderBy);

            var items = await orderedQ
                .Skip(page.Skip)
                .Take(page.Size)
                .ToListAsync(ct);
            var total = await baseQ.CountAsync(ct);
            return (items, total);
        }

        /// <summary>
        /// Server-side <c>SUM</c> over the filtered set (EF Core 6+). Replaces
        /// the "load the whole table into memory and <c>.Sum()</c>" pattern
        /// that used to dominate <c>PointService.GetBalanceAsync</c>.
        /// </summary>
        public Task<int> SumIntByConditionAsync(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, int>> selector,
            CancellationToken ct = default)
        {
            var q = _dbSet.AsNoTracking().Where(predicate);
            return q.SumAsync(selector, ct);
        }

        public Task AddAsync(T entity, CancellationToken ct = default)
        {
            _dbSet.Add(entity);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
        {
            _dbSet.AddRange(entities);
            return Task.CompletedTask;
        }

        public void Update(T entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
        }

        public Task RemoveAsync(T entity, CancellationToken ct = default)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }
    }
}
