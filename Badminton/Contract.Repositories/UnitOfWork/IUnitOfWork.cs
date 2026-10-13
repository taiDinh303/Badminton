using System.Linq.Expressions;

namespace Contract.Repositories.UnitOfWork
{
    /// <summary>
    /// UNIT OF WORK (đóng gói / encapsulation of a transactional boundary).
    /// A service pulls repositories off ONE UnitOfWork instance, so all reads
    /// and writes in a single request are committed together.
    /// </summary>
    public interface IUnitOfWork
    {
        IGenericRepository<T> GetRepository<T>() where T : class;

        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
