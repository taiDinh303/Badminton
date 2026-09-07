using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UOW
{
    public class TimeSlotRepository : ITimeSlotRepository
    {
        private readonly BadmintonBookingDbContext _context;

        public TimeSlotRepository(BadmintonBookingDbContext context)
        {
            _context = context;
        }

        public async Task<List<TimeSlot>> GetAllAsync()
        {
            return await _context.TimeSlots
                .Where(x => x.IsActive)
                .OrderBy(x => x.StartTime)
                .ToListAsync();
        }

        public async Task<TimeSlot?> GetByIdAsync(int timeSlotId)
        {
            return await _context.TimeSlots
                .FirstOrDefaultAsync(x =>
                    x.TimeSlotId == timeSlotId &&
                    x.IsActive);
        }
    }
}