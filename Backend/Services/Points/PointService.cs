using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Membership;
using Contract.Services.Points;
using Badminton.Core.Base;
using Badminton.Core.Security;

namespace Services
{
    public sealed class PointService : BaseService, IPointService
    {
        private const int PointsPerVnd = 1_000;
        private const int VndPerPoints = 20_000;

        private readonly IMembershipService _membership;

        public PointService(IUnitOfWork uow, ICurrentUser currentUser,
            IMembershipService membership)
            : base(uow, currentUser)
        {
            _membership = membership;
        }

        private IGenericRepository<PointLedger> Repo => Repository<PointLedger>();

        public Task<int> GetBalanceAsync(int userId) =>
            Repo.SumIntByConditionAsync(p => p.UserId == userId, p => p.Delta);

        public async Task<bool> HasEarnedRowAsync(int bookingId)
        {
            var n = await Repo.CountByConditionAsync(p => p.LedgerKey == $"earn:{bookingId}");
            return n > 0;
        }

        public Task<int> RedeemAsync(int userId, int bookingId, int pointsToRedeem, string bookingCode) =>
            MutateAsync(userId, () => RedeemCoreAsync(userId, bookingId, pointsToRedeem, bookingCode));

        public Task RestoreAsync(int userId, int bookingId, int pointsRestored, string bookingCode) =>
            MutateAsync(userId, () => RestoreCoreAsync(userId, bookingId, pointsRestored, bookingCode));

        public Task AwardAsync(int userId, int bookingId, int amount, string reason) =>
            MutateAsync(userId, () => AwardCoreAsync(userId, bookingId, amount, reason));

        public Task ClawbackAsync(int userId, int bookingId, int amount, string reason) =>
            MutateAsync(userId, () => ClawbackCoreAsync(userId, bookingId, amount, reason));

        private async Task<T> MutateAsync<T>(int userId, Func<Task<T>> action)
        {
            T result = default!;
            await Uow.RunInTransactionAsync(async () =>
            {
                await Uow.LockPointsUserAsync(userId);
                result = await action();
            });
            return result;
        }

        private Task MutateAsync(int userId, Func<Task> action) => MutateAsync(userId, async () =>
        {
            await action();
            return true;
        });

        private async Task<int> RedeemCoreAsync(int userId, int bookingId,
            int pointsToRedeem, string bookingCode)
        {
            if (pointsToRedeem <= 0) return 0;

            var existing = (await Repo.GetByConditionAsync(p => p.LedgerKey == $"redeem:{bookingId}"))
                .SingleOrDefault();
            if (existing is not null)
            {
                if (existing.UserId != userId) throw new ConflictException("Ledger không thuộc tài khoản này.");
                return checked((int)(-(long)existing.Delta * VndPerPoints / PointsPerVnd));
            }

            var balance = await GetBalanceAsync(userId);
            if (balance < pointsToRedeem)
                throw new ConflictException(
                    $"So diem van chan (co {balance}, can {pointsToRedeem}).");

            var discount = (int)(pointsToRedeem * (long)VndPerPoints / PointsPerVnd);

            await Repo.AddAsync(new PointLedger
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

        private async Task RestoreCoreAsync(int userId, int bookingId,
            int pointsRestored, string bookingCode)
        {
            if (pointsRestored <= 0) return;

            var already = await Repo.CountByConditionAsync(
                p => p.LedgerKey == $"restore:{bookingId}");
            if (already > 0) return;

            await Repo.AddAsync(new PointLedger
            {
                UserId = userId,
                Delta = pointsRestored,
                BookingId = bookingId,
                Reason = $"restore {pointsRestored} pts after refund {bookingCode}",
                LedgerKey = $"restore:{bookingId}"
            });
            await SaveAsync();
        }

        private async Task AwardCoreAsync(int userId, int bookingId, int amount, string reason)
        {
            if (amount <= 0) return;

            // Fast path: if the earn row already exists, do nothing.
            var already = await Repo.CountByConditionAsync(p => p.LedgerKey == $"earn:{bookingId}");
            if (already > 0) return;

            // A failed tier lookup must roll back the operation rather than silently underpay points.
            var mult = await _membership.EffectiveMultiplierAsync(userId);

            int earned = (int)Math.Round(amount * mult, MidpointRounding.AwayFromZero);

            await Repo.AddAsync(new PointLedger
            {
                UserId = userId,
                Delta = earned,
                BookingId = bookingId,
                Reason = reason,
                LedgerKey = $"earn:{bookingId}"
            });
            await SaveAsync();
        }

        private async Task ClawbackCoreAsync(int userId, int bookingId, int amount, string reason)
        {
            if (amount <= 0) return;

            var already = await Repo.CountByConditionAsync(p => p.LedgerKey == $"clawback:{bookingId}");
            if (already > 0) return;

            var earned = (await Repo.GetByConditionAsync(p => p.LedgerKey == $"earn:{bookingId}"))
                .SingleOrDefault();
            if (earned is null) return;
            var booking = await Repository<Booking>().GetByIdAsync(bookingId)
                ?? throw new NotFoundException("Booking", bookingId);
            var clawback = booking.PayableAmount <= 0 ? 0 : (int)Math.Round(
                earned.Delta * Math.Min(1m, (decimal)amount / booking.PayableAmount),
                MidpointRounding.AwayFromZero);
            await Repo.AddAsync(new PointLedger
            {
                UserId = userId,
                Delta = -clawback,
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
