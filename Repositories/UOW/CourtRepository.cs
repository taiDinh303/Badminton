using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UOW
{
    public class CourtRepository : ICourtRepository
    {
        private readonly BadmintonBookingDbContext _context;

        public CourtRepository(BadmintonBookingDbContext context)
        {
            _context = context;
        }

        public async Task<List<Court>> GetAllAsync()
        {
            return await _context.Courts
                .Where(x => x.IsActive && x.Status == "Available")
                .OrderBy(x => x.CourtId)
                .ToListAsync();
        }

        public async Task<Court?> GetByIdAsync(int courtId)
        {
            return await _context.Courts
                .FirstOrDefaultAsync(x =>
                    x.CourtId == courtId &&
                    x.IsActive &&
                    x.Status == "Available");
        }
    }
}