using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UOW
{
    public class BranchRepository : IBranchRepository
    {
        private readonly DatabaseContext _context;

        public BranchRepository(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<List<Branch>> GetAllAsync()
        {
            return await _context.Branches
                .OrderBy(x => x.BranchName)
                .ToListAsync();
        }

        public async Task<Branch?> GetByIdAsync(int id)
        {
            return await _context.Branches
                .FirstOrDefaultAsync(x => x.BranchId == id);
        }

        public async Task<Branch> CreateAsync(Branch branch)
        {
            _context.Branches.Add(branch);
            await _context.SaveChangesAsync();

            return branch;
        }

        public async Task<Branch?> UpdateAsync(Branch branch)
        {
            var existing = await _context.Branches
                .FirstOrDefaultAsync(x => x.BranchId == branch.BranchId);

            if (existing == null)
                return null;

            existing.BranchName = branch.BranchName;
            existing.Address = branch.Address;
            existing.PhoneNumber = branch.PhoneNumber;
            existing.Description = branch.Description;
            existing.IsActive = branch.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var branch = await _context.Branches
                .FirstOrDefaultAsync(x => x.BranchId == id);

            if (branch == null)
                return false;

            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}