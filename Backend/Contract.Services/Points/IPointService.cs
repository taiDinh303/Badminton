using Contract.Repositories.UnitOfWork;

namespace Contract.Services.Points
{
    public interface IPointService
    {
        Task<int> GetBalanceAsync(int userId);
        Task<bool> HasEarnedRowAsync(int bookingId);
        Task<int> RedeemAsync(int userId, int bookingId, int pointsToRedeem, string bookingCode);
        Task RestoreAsync(int userId, int bookingId, int pointsRestored, string bookingCode);
        Task AwardAsync(int userId, int bookingId, int amount, string reason);
        Task ClawbackAsync(int userId, int bookingId, int amount, string reason);
        Task<IReadOnlyList<PointEntry>> GetLedgerAsync(int userId, int limit = 50);
        Task<(IReadOnlyList<PointEntry> Items, int TotalCount)>
            GetLedgerPagedAsync(int userId, Page page);
    }

    public sealed record PointEntry(int Id, int Delta, string Reason, int? BookingId, DateTime CreatedAt);
}
