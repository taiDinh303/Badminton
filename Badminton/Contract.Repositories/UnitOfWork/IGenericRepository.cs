using System.Linq.Expressions;

namespace Contract.Repositories.UnitOfWork
{
    /// <summary>
    /// ABSTRACTION (trừu tượng): the single CRUD contract every concrete
    /// repository implements. Business services depend on this interface
    /// (and on <see cref="IUnitOfWork"/>) — never on EF Core directly — so the
    /// data-access mechanism can be swapped without touching services.
    /// </summary>
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
