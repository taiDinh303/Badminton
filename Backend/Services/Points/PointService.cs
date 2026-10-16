using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Points;
using Badminton.Core.Base;
using Microsoft.AspNetCore.Http;

namespace Services
{
    public sealed class PointService : BaseService, IPointService
    {
        /// <summary>1,000 points = 20,000 VND (docs 01 §5).</summary>
        private const int PointsPerVnd = 1_000;
        private const int VndPerPoints = 20_000;

        public PointService(IUnitOfWork uow, IHttpContextAccessor http) : base(uow, http) { }

        private IGenericRepository<PointLedger> Repo => Repository<PointLedger>();

        public Task<int> GetBalanceAsync(int userId) =>
            Repo.SumIntByConditionAsync(p => p.UserId == userId, p => p.Delta);

        public async Task<int> RedeemAsync(int userId, int bookingId,
            int pointsToRedeem, string bookingCode)
        {
            if (pointsToRedeem <= 0) return 0;

            // Idempotency: if a redeem row for this booking already exists,
            // return the same discount without writing a second row.
            var already = await Repo.CountByConditionAsync(
                p => p.LedgerKey == $"redeem:{bookingId}");
            if (already > 0)
                return (int)(pointsToRedeem * (long)VndPerPoints / PointsPerVnd);

            var balance = await GetBalanceAsync(userId);
            if (balance < pointsToRedeem)
                throw new ConflictException(
                    $"So diem van chan (co {balance}, can {pointsToRedeem}).");

            var discount = (int)(pointsToRedeem * (long)VndPerPoints / PointsPerVnd);

            _ = Repo.AddAsync(new PointLedger
            {
                UserId = userId,
                Delta = -pointsToRedeem,
                BookingId = bookingId,
                Reason = $"redeem {pointsToRedeem} pts for {bookingCode}",
                LedgerKey = $"redeem:{bookingId}"
            });
            await SaveAsync();
            return discount;
        }

        public async Task RestoreAsync(int userId, int bookingId,
            int pointsRestored, string bookingCode)
        {
            if (pointsRestored <= 0) return;

            var already = await Repo.CountByConditionAsync(
                p => p.LedgerKey == $"restore:{bookingId}");
            if (already > 0) return;

            _ = Repo.AddAsync(new PointLedger
            {
                UserId = userId,
                Delta = pointsRestored,
                BookingId = bookingId,
                Reason = $"restore {pointsRestored} pts after refund {bookingCode}",
                LedgerKey = $"restore:{bookingId}"
            });
            await SaveAsync();
        }

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

        public async Task<IReadOnlyList<PointEntry>> GetLedgerAsync(int userId, int limit = 50)
        {
            var (items, _) = await GetLedgerPagedAsync(userId, new Page(1, Math.Max(1, limit)));
            return items;
        }
    }
}
