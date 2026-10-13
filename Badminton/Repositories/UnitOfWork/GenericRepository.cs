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
            var found = await _dbSet.FindAsync([id], ct);
            return found;
        }

        public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
            await _dbSet.AsNoTracking().ToListAsync(ct);

        public async Task<IReadOnlyList<T>> GetByConditionAsync(
            Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
            await _dbSet.AsNoTracking().Where(predicate).ToListAsync(ct);

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
