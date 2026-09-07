using ModelViews.Branch;

namespace Contract.Services.Interface
{
    public interface IBranchService
    {
        Task<List<BranchResponse>> GetAllAsync();

        Task<BranchResponse?> GetByIdAsync(int id);

        Task<BranchResponse> CreateAsync(BranchRequest request);

        Task<BranchResponse?> UpdateAsync(int id, BranchRequest request);

        Task<bool> DeleteAsync(int id);
    }
}