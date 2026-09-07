using ModelViews.CalendarType;

namespace Contract.Services.Interface
{
    public interface ICalendarTypeService
    {
        Task<List<CalendarTypeResponse>> GetAllAsync();
        Task<CalendarTypeResponse?> GetByIdAsync(int id);
        Task<CalendarTypeResponse> CreateAsync(CalendarTypeRequest request);
        Task<CalendarTypeResponse?> UpdateAsync(int id, CalendarTypeRequest request);
        Task<bool> DeleteAsync(int id);
    }
}