using Contract.Repositories.Entity;

namespace Contract.Repositories.IOUW
{
    public interface ICalendarTypeRepository
    {
        Task<List<CalendarType>> GetAllAsync();
        Task<CalendarType?> GetByIdAsync(int id);
        Task<CalendarType> CreateAsync(CalendarType calendarType);
        Task<CalendarType?> UpdateAsync(CalendarType calendarType);
        Task<bool> DeleteAsync(int id);
    }
}