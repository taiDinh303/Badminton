using ModelViews.Court;

namespace Contract.Services.Court
{
    public interface ICourtService
    {
        Task<IReadOnlyList<CourtView>> GetAllAsync(int? branchId = null);
        Task<CourtView> GetByIdAsync(int id);
        Task<int> CreateAsync(CreateCourtRequest request, int actorId);
        Task UpdateAsync(int id, UpdateCourtRequest request, int actorId);
        Task<ModelViews.Booking.ConfigurationImpact> GetImpactAsync(int id);
        Task DeleteAsync(int id, int actorId);
    }
}
