using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Report;
using M.Core.Enums;
using Microsoft.Extensions.Caching.Memory;

namespace Services
{
    public sealed class ReportService : BaseService, IReportService
    {
        private readonly IMemoryCache _cache;
        // 5-minute TTL (docs: Redis ≤5m for volatile data; MemoryCache keeps it
        // dependency-free in dev. Swap for ICache in prod without touching callers.)
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        public ReportService(IUnitOfWork uow, Microsoft.AspNetCore.Http.IHttpContextAccessor http,
            IMemoryCache cache)
            : base(uow, http) { _cache = cache; }

        public async Task<DashboardSummary> GetSummaryAsync(int? branchId = null)
        {
            var key = $"report:summary:{branchId?.ToString() ?? "all"}";
            if (_cache.TryGetValue(key, out DashboardSummary? hit)) return hit!;
            var result = await LoadSummaryAsync(branchId);
            _cache.Set(key, result, CacheTtl);
            return result;
        }

        private async Task<DashboardSummary> LoadSummaryAsync(int? branchId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var thirtyAgo = today.AddDays(-30);
            var bi = branchId;

            var bookings = Repository<Booking>();
            var users = Repository<User>();

            int todayCount = await bookings.CountByConditionAsync(b =>
                b.DeletedAt == null && b.Date == today && (bi == null || b.BranchId == bi));
            int pending = await bookings.CountByConditionAsync(b =>
                b.DeletedAt == null && b.Status == BookingStatus.Pending && (bi == null || b.BranchId == bi));
            int noShow30 = await bookings.CountByConditionAsync(b =>
                b.DeletedAt == null && b.Date >= thirtyAgo && b.Status == BookingStatus.NoShow && (bi == null || b.BranchId == bi));
            int revenueToday = await bookings.SumIntByConditionAsync(
                b => b.DeletedAt == null && b.Date == today && b.Status != BookingStatus.Cancelled && (bi == null || b.BranchId == bi),
                b => b.PayableAmount);
            int usersToday = await users.CountByConditionAsync(u =>
                u.DeletedAt == null
                && u.CreatedAt >= today.ToDateTime(TimeOnly.MinValue)
                && u.CreatedAt < today.AddDays(1).ToDateTime(TimeOnly.MinValue)
                && (bi == null || u.BranchId == null || u.BranchId == bi));
            int activeCourts = await Repository<Court>().CountByConditionAsync(c =>
                c.IsActive && c.DeletedAt == null && (bi == null || c.BranchId == bi));
            int activeBranches = await Repository<Branch>().CountByConditionAsync(b =>
                b.DeletedAt == null && b.Status == BranchStatus.Active);

            // Peak fill rate for today: booked peak slots / (active courts x peak slots).
            var peakSlots = await Repository<TimeSlot>().GetByConditionAsync(s => s.IsActive && s.IsPeak && s.DeletedAt == null);
            int peakTotal = Math.Max(0, peakSlots.Count * activeCourts);
            int peakBooked = await bookings.CountByConditionAsync(b =>
                b.DeletedAt == null && b.Date == today
                && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.NoShow
                && (bi == null || b.BranchId == bi));
            decimal pct = peakTotal == 0 ? 0m : Math.Round(100m * (decimal)peakBooked / peakTotal, 1);

            return new DashboardSummary(todayCount, pending, noShow30, revenueToday,
                pct, usersToday, activeCourts, activeBranches);
        }

        public async Task<IReadOnlyList<RevenueByDay>> GetRevenueAsync(
            DateOnly from, DateOnly to, int? branchId = null)
        {
            var key = $"report:revenue:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{branchId?.ToString() ?? "all"}";
            if (_cache.TryGetValue(key, out IReadOnlyList<RevenueByDay>? revHit)) return revHit!;
            var bi = branchId;
            var bookings = await Repository<Booking>().GetByConditionAsync(b =>
                b.DeletedAt == null
                && b.Date >= from && b.Date <= to
                && b.Status != BookingStatus.Cancelled
                && (bi == null || b.BranchId == bi));
            var byDay = bookings
                .GroupBy(b => b.Date)
                .Select(g => new RevenueByDay(
                    g.Key,
                    g.Count(),
                    g.Sum(x => x.PayableAmount),
                    0))
                .OrderBy(g => g.Date)
                .ToList();

            // Refunds (separate table; no DeletedAt on Payment).
            var fromDt = from.ToDateTime(TimeOnly.MinValue);
            var toEnd = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var pmts = await Repository<Payment>().GetByConditionAsync(p =>
                p.RefundedAt.HasValue && p.RefundedAt!.Value >= fromDt && p.RefundedAt.Value < toEnd);
            var refundByDay = pmts
                .Where(p => p.RefundedAt.HasValue)
                .GroupBy(p => DateOnly.FromDateTime(p.RefundedAt!.Value))
                .ToDictionary(g => g.Key, g => g.Sum(x => x.RefundAmount));

            var result = byDay.Select(r => new RevenueByDay(
                r.Date, r.Bookings, r.Revenue,
                refundByDay.TryGetValue(r.Date, out var v) ? v : 0)).ToList();
            _cache.Set(key, result, CacheTtl);
            return result;
        }

        public async Task<IReadOnlyList<OccupancyByDay>> GetOccupancyAsync(
            DateOnly from, DateOnly to, int? branchId = null)
        {
            var key = $"report:occupancy:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{branchId?.ToString() ?? "all"}";
            if (_cache.TryGetValue(key, out IReadOnlyList<OccupancyByDay>? occHit)) return occHit!;
            var bi = branchId;
            var courts = await Repository<Court>().GetByConditionAsync(c =>
                c.IsActive && c.DeletedAt == null && (bi == null || c.BranchId == bi));
            var slots = await Repository<TimeSlot>().GetByConditionAsync(s => s.IsActive && s.DeletedAt == null);
            var branches = await Repository<Branch>().GetByConditionAsync(b => b.DeletedAt == null);
            var booked = await Repository<Booking>().GetByConditionAsync(b =>
                b.DeletedAt == null && b.Status != BookingStatus.Cancelled
                && b.Date >= from && b.Date <= to
                && (bi == null || b.BranchId == bi));

            int totalSlotsDay = Math.Max(1, courts.Count * slots.Count);
            int peakSlotsDay = Math.Max(1, courts.Count * slots.Count(s => s.IsPeak));
            string? name = bi == null
                ? (branches.Count(b => b.Status == BranchStatus.Active) > 0 ? "Tất cả cơ sở" : null)
                : branches.FirstOrDefault(b => b.Id == bi)?.Name;

            var list = new List<OccupancyByDay>();
            var d = from;
            while (d <= to)
            {
                var dayBooked = booked.Count(b => b.Date == d);
                var dayPeak = booked.Count(b => b.Date == d);
                list.Add(new OccupancyByDay(
                    d, bi, name,
                    dayBooked, totalSlotsDay,
                    Math.Round(100m * (decimal)dayBooked / totalSlotsDay, 1),
                    dayPeak, peakSlotsDay,
                    Math.Round(100m * (decimal)dayPeak / peakSlotsDay, 1)));
                d = d.AddDays(1);
            }
            _cache.Set(key, list, CacheTtl);
            return list;
        }
    }
}

