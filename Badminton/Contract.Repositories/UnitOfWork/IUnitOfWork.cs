using System.Linq.Expressions;

namespace Contract.Repositories.UnitOfWork
{
    public interface IUnitOfWork
    {
        IGenericRepository<T> GetRepository<T>() where T : class;

        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
