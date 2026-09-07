using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Contract.Services.Interface;
using ModelViews.Branch;

namespace Services.Service
{
    public class BranchService : IBranchService
    {
        private readonly IUOW _uow;

        public BranchService(IUOW uow)
        {
            _uow = uow;
        }

        public async Task<List<BranchResponse>> GetAllAsync()
        {
            var branches = await _uow.Branches.GetAllAsync();

            return branches
                .Select(MapToResponse)
                .ToList();
        }

        public async Task<BranchResponse?> GetByIdAsync(int id)
        {
            var branch = await _uow.Branches.GetByIdAsync(id);

            if (branch == null)
                return null;

            return MapToResponse(branch);
        }

        public async Task<BranchResponse> CreateAsync(BranchRequest request)
        {
            var branch = new Branch
            {
                BranchName = request.BranchName,
                Address = request.Address,
                PhoneNumber = request.PhoneNumber,
                Description = request.Description,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _uow.Branches.CreateAsync(branch);

            return MapToResponse(result);
        }

        public async Task<BranchResponse?> UpdateAsync(
            int id,
            BranchRequest request)
        {
            var branch = await _uow.Branches.GetByIdAsync(id);

            if (branch == null)
                return null;

            branch.BranchName = request.BranchName;
            branch.Address = request.Address;
            branch.PhoneNumber = request.PhoneNumber;
            branch.Description = request.Description;
            branch.IsActive = request.IsActive;

            var result = await _uow.Branches.UpdateAsync(branch);

            if (result == null)
                return null;

            return MapToResponse(result);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _uow.Branches.DeleteAsync(id);
        }

        private static BranchResponse MapToResponse(Branch branch)
        {
            return new BranchResponse
            {
                BranchId = branch.BranchId,
                BranchName = branch.BranchName,
                Address = branch.Address,
                PhoneNumber = branch.PhoneNumber,
                Description = branch.Description,
                IsActive = branch.IsActive,
                CreatedAt = branch.CreatedAt,
                UpdatedAt = branch.UpdatedAt
            };
        }
    }
}