using ModelViews.Branch;

namespace Contract.Services.Branch
{
    public interface IBranchService
    {
        Task<IReadOnlyList<BranchView>> GetAllAsync();
        Task<BranchView> GetByIdAsync(int id);
        Task<int> CreateAsync(CreateBranchRequest request, int actorId);
        Task UpdateAsync(int id, UpdateBranchRequest request, int actorId);
        Task<ModelViews.Booking.ConfigurationImpact> GetImpactAsync(int id);
        Task DeleteAsync(int id, int actorId);
    }
}
