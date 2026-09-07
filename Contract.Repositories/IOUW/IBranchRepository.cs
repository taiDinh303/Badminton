using Contract.Repositories.Entity;

namespace Contract.Repositories.IOUW
{
    public interface IBranchRepository
    {
        Task<List<Branch>> GetAllAsync();

        Task<Branch?> GetByIdAsync(int id);

        Task<Branch> CreateAsync(Branch branch);

        Task<Branch?> UpdateAsync(Branch branch);

        Task<bool> DeleteAsync(int id);
    }
}