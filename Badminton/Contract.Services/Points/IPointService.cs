using Contract.Repositories.UnitOfWork;

namespace Contract.Services.Points
{
    public interface IPointService
    {
        /// <summary>Server-side SUM of the user's ledger (no full-table load).</summary>
        Task<int> GetBalanceAsync(int userId);

        /// <summary>Idempotent earn, keyed by booking (unique LedgerKey backstop).</summary>
        Task AwardAsync(int userId, int bookingId, int amount, string reason);

        /// <summary>Idempotent clawback, keyed by booking.</summary>
        Task ClawbackAsync(int userId, int bookingId, int amount, string reason);

        /// <summary>Backward-compatible capped page of the ledger.</summary>
        Task<IReadOnlyList<PointEntry>> GetLedgerAsync(int userId, int limit = 50);

        /// <summary>Paged ledger, newest first, with total for the caller.</summary>
        Task<(IReadOnlyList<PointEntry> Items, int TotalCount)>
            GetLedgerPagedAsync(int userId, Page page);
    }

    public sealed record PointEntry(int Id, int Delta, string Reason, int? BookingId, DateTime CreatedAt);
}
