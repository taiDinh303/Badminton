using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Security;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Report;

namespace Services;

public sealed class ReportService(IUnitOfWork uow, ICurrentUser currentUser) : BaseService(uow, currentUser), IReportService
{
    private static (DateTime Start, DateTime End) Range(DateOnly from, DateOnly to)
    {
        if (to < from || to.DayNumber - from.DayNumber > 366)
            throw new BadRequestException("Khoảng báo cáo phải hợp lệ và không quá 367 ngày.");
        return (DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).Add(-HanoiTime.FixedOffset), DateTimeKind.Utc),
            DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).Add(-HanoiTime.FixedOffset), DateTimeKind.Utc));
    }

    public async Task<DashboardSummary> GetSummaryAsync(int? branchId = null)
    {
        var branch = await ResolveStaffBranchAsync(branchId);
        var today = HanoiTime.HanoiToday();
        var now = TimeOnly.FromDateTime(HanoiTime.HanoiNow());
        var (start, end) = Range(today, today);
        var revenue = await LoadRevenueAsync(today, today, branch);
        var occupancy = await LoadOccupancyAsync(today, today, branch);
        var bookings = Repository<Booking>();
        var users = Repository<User>();
        return new DashboardSummary(
            await bookings.CountByConditionAsync(b => b.DeletedAt == null && b.Date == today && (branch == null || b.BranchId == branch)),
            await bookings.CountByConditionAsync(b => b.DeletedAt == null && b.Status == BookingStatus.Pending && (branch == null || b.BranchId == branch)),
            await bookings.CountByConditionAsync(b => b.DeletedAt == null && b.Date >= today.AddDays(-30)
                && b.Status == BookingStatus.NoShow && (branch == null || b.BranchId == branch)),
            revenue.Sum(r => r.Revenue - r.Refunds), occupancy[0].PeakFillRatePct,
            await users.CountByConditionAsync(u => u.DeletedAt == null && u.CreatedAt >= start && u.CreatedAt < end
                && (branch == null || u.BranchId == branch)),
            await Repository<Court>().CountByConditionAsync(c => c.IsActive && c.DeletedAt == null && (branch == null || c.BranchId == branch)),
            await Repository<Branch>().CountByConditionAsync(b => b.IsActive && b.DeletedAt == null && b.Status == BranchStatus.Active
                && (branch == null || b.Id == branch)),
            await users.CountByConditionAsync(u => u.DeletedAt == null && u.Role == Role.Customer
                && (u.Tier == MembershipTier.Gold || u.Tier == MembershipTier.Silver)
                && u.MembershipExpiresAt > DateTime.UtcNow && (branch == null || u.BranchId == branch)),
            await Repository<WaitlistEntry>().CountByConditionAsync(w => w.DeletedAt == null && w.Status == WaitlistStatus.Waiting
                && (branch == null || w.BranchId == branch)),
            await bookings.CountByConditionAsync(b => b.DeletedAt == null && b.Status == BookingStatus.Confirmed && b.CheckedInAt == null
                && (branch == null || b.BranchId == branch) && b.Slot != null
                && (b.Date < today || (b.Date == today && b.Slot.EndTime <= now))));
    }

    public async Task<IReadOnlyList<RevenueByDay>> GetRevenueAsync(DateOnly from, DateOnly to, int? branchId = null) =>
        await LoadRevenueAsync(from, to, await ResolveStaffBranchAsync(branchId));

    private async Task<IReadOnlyList<RevenueByDay>> LoadRevenueAsync(DateOnly from, DateOnly to, int? branch)
    {
        var (start, end) = Range(from, to);
        var payments = await Repository<Payment>().GetByConditionAsync(p =>
            (p.Status == PaymentStatus.Success && p.PaidAt >= start && p.PaidAt < end)
            || ((p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartialRefund)
                && p.RefundedAt >= start && p.RefundedAt < end));
        if (branch.HasValue)
        {
            var ids = payments.Select(p => p.BookingId).Distinct().ToList();
            var allowed = (await Repository<Booking>().GetByConditionAsync(b => ids.Contains(b.Id) && b.BranchId == branch))
                .Select(b => b.Id).ToHashSet();
            payments = payments.Where(p => allowed.Contains(p.BookingId)).ToList();
        }
        var paid = payments.Where(p => p.Status == PaymentStatus.Success && p.PaidAt.HasValue)
            .GroupBy(p => HanoiTime.HanoiDateOf(p.PaidAt!.Value)).ToDictionary(g => g.Key, g => g.ToList());
        var refunded = payments.Where(p => p.RefundedAt.HasValue)
            .GroupBy(p => HanoiTime.HanoiDateOf(p.RefundedAt!.Value)).ToDictionary(g => g.Key, g => g.Sum(p => p.RefundAmount));
        // Include zero and refund-only days. Revenue remains gross for HTTP compatibility; net = Revenue - Refunds.
        var rows = new List<RevenueByDay>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            paid.TryGetValue(day, out var receipts);
            rows.Add(new(day, receipts?.Select(p => p.BookingId).Distinct().Count() ?? 0,
                receipts?.Sum(p => p.Amount) ?? 0, refunded.GetValueOrDefault(day)));
        }
        return rows;
    }

    public async Task<IReadOnlyList<OccupancyByDay>> GetOccupancyAsync(DateOnly from, DateOnly to, int? branchId = null) =>
        await LoadOccupancyAsync(from, to, await ResolveStaffBranchAsync(branchId));

    private async Task<IReadOnlyList<OccupancyByDay>> LoadOccupancyAsync(DateOnly from, DateOnly to, int? branch)
    {
        Range(from, to);
        var branches = (await Repository<Branch>().GetByConditionAsync(b => branch == null || b.Id == branch)).ToDictionary(b => b.Id);
        var snapshots = await Repository<InventoryDaySnapshot>().GetByConditionAsync(x => x.Date >= from && x.Date <= to && (branch == null || x.BranchId == branch));
        var snapshotMap = snapshots.ToDictionary(x => (x.BranchId, x.Date));
        var bookings = await Repository<Booking>().GetByConditionAsync(b => b.DeletedAt == null && b.Date >= from && b.Date <= to
            && (branch == null || b.BranchId == branch) && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed));
        var courts = await Repository<Court>().GetByConditionAsync(c => c.IsActive && c.DeletedAt == null && (branch == null || c.BranchId == branch));
        var courtIds = courts.Select(c => c.Id).ToList();
        var slots = await Repository<TimeSlot>().GetByConditionAsync(s => s.IsActive && s.DeletedAt == null);
        var schedules = (await Repository<CourtDateStatusEntry>().GetByConditionAsync(s => courtIds.Contains(s.CourtId) && s.Date >= from && s.Date <= to))
            .ToDictionary(s => (s.CourtId, s.Date));
        var rows = new List<OccupancyByDay>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var available = new List<InventorySlot>();
            var estimated = false;
            foreach (var owner in branches.Values)
            {
                if (day < HanoiTime.HanoiToday() && snapshotMap.TryGetValue((owner.Id, day), out var snapshot))
                    available.AddRange(System.Text.Json.JsonSerializer.Deserialize<List<InventorySlot>>(snapshot.CapacityJson) ?? []);
                else {
                    if (owner.IsActive && owner.DeletedAt == null && owner.Status == BranchStatus.Active)
                        foreach (var court in courts.Where(c => c.BranchId == owner.Id)) {
                            schedules.TryGetValue((court.Id, day), out var schedule);
                            available.AddRange(slots.Where(s => s.StartTime >= owner.OpenTime && s.EndTime <= owner.CloseTime
                                && schedule?.Blocks(s.StartTime, s.EndTime) != true).Select(s => new InventorySlot(court.Id, s.Id, s.IsPeak)));
                        }
                    if (day < HanoiTime.HanoiToday()) estimated = true;
                }
            }
            var capacity = available.Select(x => (x.Court, x.Slot)).ToHashSet();
            var peak = available.Where(x => x.Peak).Select(x => (x.Court, x.Slot)).ToHashSet();
            var occupied = bookings.Where(b => b.Date == day && capacity.Contains((b.CourtId, b.SlotId)))
                .Select(b => (Court: b.CourtId, Slot: b.SlotId)).Distinct().ToList();
            var peakOccupied = occupied.Count(x => peak.Contains(x));
            rows.Add(new(day, branch, branch is { } id ? branches.GetValueOrDefault(id)?.Name : "Tất cả cơ sở",
                occupied.Count, capacity.Count, Percent(occupied.Count, capacity.Count), peakOccupied, peak.Count, Percent(peakOccupied, peak.Count), estimated));
        }
        return rows;
    }

    private static decimal Percent(int count, int total) => total == 0 ? 0 : Math.Round(100m * count / total, 1);
}
