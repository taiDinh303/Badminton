using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UOW
{
    public class CourtTypeRepository : ICourtTypeRepository
    {
        private readonly BadmintonBookingDbContext _context;

        public CourtTypeRepository(BadmintonBookingDbContext context)
        {
            _context = context;
        }

        public async Task<List<CourtType>> GetAllAsync()
        {
            return await _context.CourtTypes
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<CourtType?> GetByIdAsync(int id)
        {
            return await _context.CourtTypes
                .FirstOrDefaultAsync(x => x.CourtTypeId == id);
        }

        public async Task<CourtType> CreateAsync(CourtType courtType)
        {
            _context.CourtTypes.Add(courtType);
            await _context.SaveChangesAsync();

            return courtType;
        }

        public async Task<CourtType?> UpdateAsync(CourtType courtType)
        {
            var existing = await _context.CourtTypes
                .FirstOrDefaultAsync(x => x.CourtTypeId == courtType.CourtTypeId);

            if (existing == null)
                return null;

            existing.Name = courtType.Name;
            existing.Description = courtType.Description;
            existing.IsActive = courtType.IsActive;

            await _context.SaveChangesAsync();

            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var courtType = await _context.CourtTypes
                .FirstOrDefaultAsync(x => x.CourtTypeId == id);

            if (courtType == null)
                return false;

            _context.CourtTypes.Remove(courtType);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
