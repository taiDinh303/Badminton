namespace Contract.Repositories.UnitOfWork
{
    public interface IUnitOfWork
    {
        IBookingCommands Bookings { get; }
        IPaymentCommands Payments { get; }
        IVoucherCommands Vouchers { get; }
        IGenericRepository<T> GetRepository<T>() where T : class;

        Task<int> SaveChangesAsync(CancellationToken ct = default);

        Task RunInTransactionAsync(Func<Task> action, CancellationToken ct = default);

        Task LockCourtDateAsync(int courtId, DateOnly date, CancellationToken ct = default);

        Task LockVoucherUserAsync(int userId, CancellationToken ct = default);

        Task LockPointsUserAsync(int userId, CancellationToken ct = default);
        Task LockBookingRequestAsync(string key, CancellationToken ct = default);
        Task LockTimeSlotsAsync(CancellationToken ct = default);
        Task LockNotificationAsync(string ledgerKey, CancellationToken ct = default);
        Task LockGroupAsync(int groupId, CancellationToken ct = default);
    }
}
