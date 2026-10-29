using Contract.Repositories.Entity;
namespace Contract.Repositories.UnitOfWork;

public interface IOperationsReadStore
{
    Task<IReadOnlyList<BookingCase>> CasesAsync(int? userId, int? branchId, int? bookingId, Page page);
    Task<IReadOnlyList<Notification>> DeliveriesAsync(int? branchId, Page page);
    Task<IReadOnlyList<ReviewReport>> ReviewReportsAsync(int? branchId, Page page);
    Task<int> ReturningCustomersAsync(int[] userIds, DateOnly before, int? branchId);
}
