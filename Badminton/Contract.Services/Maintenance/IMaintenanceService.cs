using M.Core.Enums;

namespace Contract.Services.Maintenance
{
    public interface IMaintenanceService
    {
        Task SetAsync(int courtId, DateOnly date, CourtDateStatus status,
            int actorId, string? note = null);
        Task<CourtDateStatus> GetStatusAsync(int courtId, DateOnly date);
    }
}
