using System.Linq.Expressions;

namespace Contract.Repositories.UnitOfWork
{
    public sealed class Page
    {
        public int Number { get; }
        public int Size { get; }

        public Page(int number, int size)
        {
            Number = Math.Max(1, number);
            Size = Math.Max(1, size);
        }

        public int Skip => (Number - 1) * Size;

        public static Page Default { get; } = new(1, 50);

        public override string ToString() => $"{Number}/{Size}";
    }

    public enum OrderDirection { Asc = 0, Desc = 1 }

    public interface IGenericRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

        Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

        Task<IReadOnlyList<T>> GetByConditionAsync(
            Expression<Func<T, bool>> predicate, CancellationToken ct = default);

        Task<int> CountByConditionAsync(
            Expression<Func<T, bool>> predicate, CancellationToken ct = default);

        Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, DateTime>> orderBy,
            OrderDirection direction,
            Page page,
            CancellationToken ct = default);

        Task<int> SumIntByConditionAsync(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, int>> selector,
            CancellationToken ct = default);

        Task AddAsync(T entity, CancellationToken ct = default);

        Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

        void Update(T entity);

        Task RemoveAsync(T entity, CancellationToken ct = default);
    }
}
