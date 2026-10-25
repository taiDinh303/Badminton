using ModelViews.Booking;

namespace Contract.Services.Report
{
    public interface IReportService
    {
        /// KPI panel for the "Tổng quan" dashboard.
        Task<DashboardSummary> GetSummaryAsync(int? branchId = null);

        /// Daily revenue for a date range (admin "Báo cáo").
        Task<IReadOnlyList<RevenueByDay>> GetRevenueAsync(DateOnly from, DateOnly to, int? branchId = null);

        /// Slot occupancy per branch per day (peak vs off-peak).
        Task<IReadOnlyList<OccupancyByDay>> GetOccupancyAsync(DateOnly from, DateOnly to, int? branchId = null);
    }

    public record DashboardSummary(
        int TodayBookings,
        int PendingCount,
        int NoShowCount30d,
        int RevenueToday,
        decimal PeakFillRatePct,
        int UsersToday,
        int ActiveCourts,
        int ActiveBranches,
        // FEAT KPIs (default 0 so older call sites keep compiling).
        int ActiveMembers = 0,
        int WaitingListCount = 0, int AttendanceToReview = 0);

    public record RevenueByDay(DateOnly Date, int Bookings, int Revenue, int Refunds);

    public record OccupancyByDay(
        DateOnly Date, int? BranchId, string? BranchName,
        int BookedSlots, int TotalSlots, decimal FillRatePct,
        int PeakBooked, int PeakTotal, decimal PeakFillRatePct, bool HistoricalEstimate = false);
}
