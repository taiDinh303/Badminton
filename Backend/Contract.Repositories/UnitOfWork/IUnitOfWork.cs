namespace Contract.Repositories.UnitOfWork
{
    public interface IUnitOfWork
    {
        IGenericRepository<T> GetRepository<T>() where T : class;

        Task<int> SaveChangesAsync(CancellationToken ct = default);

        Task RunInTransactionAsync(Func<Task> action, CancellationToken ct = default);

        Task<int> ExecuteSqlRawAsync(string sql, params object[] sqlParameters);

        Task LockCourtDateAsync(int courtId, DateOnly date, CancellationToken ct = default);

        Task LockVoucherUserAsync(int userId, CancellationToken ct = default);
    }
}
