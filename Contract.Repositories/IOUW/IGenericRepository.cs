using System.Linq.Expressions;
using NTC.Core.Base;


namespace NTC.Contract.Repositories.IUOW
{
    public interface IGenericRepository<T> where T : class
    {
        // query
        IQueryable<T> Entities { get; }

        // non async
        IEnumerable<T> GetAll();
        T? GetById(object id);
        void Insert(T obj);
        void InsertRange(IList<T> obj);
        void Update(T obj);
        void Delete(T obj);
        void Save();

        // async
        Task<T?> FindByConditionAsync(Expression<Func<T, bool>> predicate);
        Task<IList<T>> GetAllAsync();
        Task<BasePaginatedList<T>> GetPagging(IQueryable<T> query, int index, int pageSize);
        Task<T?> GetByIdAsync(object id);
        Task InsertAsync(T obj);
        Task UpdateAsync(T obj);
        Task DeleteAsync(T obj);
        Task SaveAsync();
    }
}
