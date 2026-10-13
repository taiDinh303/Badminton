namespace Contract.Services.Points
{
    public interface IPointService
    {
        Task<int> GetBalanceAsync(int userId);

        Task AwardAsync(int userId, int bookingId, int amount, string reason);

        Task ClawbackAsync(int userId, int bookingId, int amount, string reason);

        Task<IReadOnlyList<PointEntry>> GetLedgerAsync(int userId, int limit = 50);
    }

    public sealed record PointEntry(int Id, int Delta, string Reason, int? BookingId, DateTime CreatedAt);
}
