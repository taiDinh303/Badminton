using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UOW
{
    public class CalendarTypeRepository : ICalendarTypeRepository
    {
        private readonly BadmintonBookingDbContext _context;

        public CalendarTypeRepository(BadmintonBookingDbContext context)
        {
            _context = context;
        }

        public async Task<List<CalendarType>> GetAllAsync()
        {
            return await _context.CalendarTypes
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<CalendarType?> GetByIdAsync(int id)
        {
            return await _context.CalendarTypes
                .FirstOrDefaultAsync(x => x.CalendarTypeId == id);
        }

        public async Task<CalendarType> CreateAsync(
            CalendarType calendarType)
        {
            _context.CalendarTypes.Add(calendarType);
            await _context.SaveChangesAsync();

            return calendarType;
        }

        public async Task<CalendarType?> UpdateAsync(
            CalendarType calendarType)
        {
            var existing = await _context.CalendarTypes
                .FirstOrDefaultAsync(
                    x => x.CalendarTypeId == calendarType.CalendarTypeId);

            if (existing == null)
                return null;

            existing.Name = calendarType.Name;
            existing.Description = calendarType.Description;
            existing.IsActive = calendarType.IsActive;

            await _context.SaveChangesAsync();

            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var calendarType = await _context.CalendarTypes
                .FirstOrDefaultAsync(x => x.CalendarTypeId == id);

            if (calendarType == null)
                return false;

            _context.CalendarTypes.Remove(calendarType);
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
