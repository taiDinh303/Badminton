using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Points;
using M.Core.Base;
using Microsoft.AspNetCore.Http;

namespace Services
{
    public sealed class PointService : BaseService, IPointService
    {
        public PointService(IUnitOfWork uow, IHttpContextAccessor http) : base(uow, http) { }

        private IGenericRepository<PointLedger> Repo => Repository<PointLedger>();

        public async Task<int> GetBalanceAsync(int userId)
        {
            var all = await Repo.GetByConditionAsync(p => p.UserId == userId);
            return all.Sum(p => p.Delta);
        }

        public async Task AwardAsync(int userId, int bookingId, int amount, string reason)
        {
            if (amount <= 0) return;
            _ = Repo.AddAsync(new PointLedger
            {
                UserId = userId,
                Delta = amount,
                BookingId = bookingId,
                Reason = reason
            });
            await SaveAsync();
        }

        public async Task ClawbackAsync(int userId, int bookingId, int amount, string reason)
        {
            if (amount <= 0) return;
            _ = Repo.AddAsync(new PointLedger
            {
                UserId = userId,
                Delta = -amount,
                BookingId = bookingId,
                Reason = reason
            });
            await SaveAsync();
        }

        public async Task<IReadOnlyList<PointEntry>> GetLedgerAsync(int userId, int limit = 50)
        {
            var all = await Repo.GetByConditionAsync(p => p.UserId == userId);
            return all
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .Select(p => new PointEntry(p.Id, p.Delta, p.Reason, p.BookingId, p.CreatedAt))
                .ToList();
        }
    }
}
