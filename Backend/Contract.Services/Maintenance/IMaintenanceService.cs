using Badminton.Core.Enums;

namespace Contract.Services.Maintenance
{
    public sealed record MaintenanceView(CourtDateStatus Status, string? StartTime, string? EndTime, string? Note);

    public interface IMaintenanceService
    {
        Task SetAsync(int courtId, DateOnly date, CourtDateStatus status,
            int actorId, string? note = null, TimeSpan? startTime = null, TimeSpan? endTime = null);
        Task<MaintenanceView> GetScheduleAsync(int courtId, DateOnly date);
        Task<CourtDateStatus> GetStatusAsync(int courtId, DateOnly date);
    }
}
