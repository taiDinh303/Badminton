using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Points;
using M.Core.Base;
using Microsoft.AspNetCore.Http;

namespace Services
{
    public sealed class PointService : BaseService, IPointService
    {
        public PointService(IUnitOfWork uow, IHttpContextAccessor http) : base(uow, http) { }

        private IGenericRepository<PointLedger> Repo => Repository<PointLedger>();

        /// <summary>
        /// Balance = server-side <c>SUM(Delta)</c> for this user's ledger.
        /// No materialising the whole ledger into memory.
        /// </summary>
        public Task<int> GetBalanceAsync(int userId) =>
            Repo.SumIntByConditionAsync(p => p.UserId == userId, p => p.Delta);

        /// <summary>
        /// Idempotent award. The ledger row is keyed by a deterministic
        /// <c>LedgerKey</c> ("earn:{bookingId}"); the unique index guarantees
        /// a replayed webhook / double confirm cannot credit the same booking
        /// twice. The add+save joins the ambient transaction (when one is
        /// open) so a conflict rolls back with the caller.
        /// </summary>
        public async Task AwardAsync(int userId, int bookingId, int amount, string reason)
        {
            if (amount <= 0) return;

            // Fast path: if the earn row already exists, do nothing.
            var already = await Repo.CountByConditionAsync(p => p.LedgerKey == $"earn:{bookingId}");
            if (already > 0) return;

            _ = Repo.AddAsync(new PointLedger
            {
                UserId = userId,
                Delta = amount,
                BookingId = bookingId,
                Reason = reason,
                LedgerKey = $"earn:{bookingId}"
            });
            await SaveAsync();
        }

        /// <summary>
        /// Idempotent clawback, mirroring <see cref="AwardAsync"/>.
        /// </summary>
        public async Task ClawbackAsync(int userId, int bookingId, int amount, string reason)
        {
            if (amount <= 0) return;

            var already = await Repo.CountByConditionAsync(p => p.LedgerKey == $"clawback:{bookingId}");
            if (already > 0) return;

            _ = Repo.AddAsync(new PointLedger
            {
                UserId = userId,
                Delta = -amount,
                BookingId = bookingId,
                Reason = reason,
                LedgerKey = $"clawback:{bookingId}"
            });
            await SaveAsync();
        }

        /// <summary>
        /// Paged ledger, newest first. Replaces "load all rows and
        /// <c>.Take(limit)</c>" on the client.
        /// </summary>
        public async Task<(IReadOnlyList<PointEntry> Items, int TotalCount)>
            GetLedgerPagedAsync(int userId, Page page)
        {
            var (rows, total) = await Repo.GetPagedAsync(
                p => p.UserId == userId,
                p => p.CreatedAt,
                OrderDirection.Desc,
                page);
            var items = rows
                .Select(p => new PointEntry(p.Id, p.Delta, p.Reason, p.BookingId, p.CreatedAt))
                .ToList();
            return (items, total);
        }

        /// <summary>
        /// Backward-compatible overload: a single capped page.
        /// </summary>
        public async Task<IReadOnlyList<PointEntry>> GetLedgerAsync(int userId, int limit = 50)
        {
            var (items, _) = await GetLedgerPagedAsync(userId, new Page(1, Math.Max(1, limit)));
            return items;
        }
    }
}
