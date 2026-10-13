using System.Linq.Expressions;

namespace Contract.Repositories.UnitOfWork
{
    public interface IGenericRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

        Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

        Task<IReadOnlyList<T>> GetByConditionAsync(
            Expression<Func<T, bool>> predicate, CancellationToken ct = default);

        Task AddAsync(T entity, CancellationToken ct = default);

        Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

        void Update(T entity);

        Task RemoveAsync(T entity, CancellationToken ct = default);
    }
}
