using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Contract.Services.Interface;
using ModelViews.CalendarType;

namespace Services.Service
{
    public class CalendarTypeService : ICalendarTypeService
    {
        private readonly IUOW _uow;

        public CalendarTypeService(IUOW uow)
        {
            _uow = uow;
        }

        public async Task<List<CalendarTypeResponse>> GetAllAsync()
        {
            var calendarTypes = await _uow.CalendarTypes.GetAllAsync();

            return calendarTypes
                .Select(MapToResponse)
                .ToList();
        }

        public async Task<CalendarTypeResponse?> GetByIdAsync(int id)
        {
            var calendarType = await _uow.CalendarTypes.GetByIdAsync(id);

            if (calendarType == null)
                return null;

            return MapToResponse(calendarType);
        }

        public async Task<CalendarTypeResponse> CreateAsync(
            CalendarTypeRequest request)
        {
            var calendarType = new CalendarType
            {
                Name = request.Name,
                Description = request.Description,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _uow.CalendarTypes.CreateAsync(calendarType);

            return MapToResponse(result);
        }

        public async Task<CalendarTypeResponse?> UpdateAsync(
            int id,
            CalendarTypeRequest request)
        {
            var calendarType = await _uow.CalendarTypes.GetByIdAsync(id);

            if (calendarType == null)
                return null;

            calendarType.Name = request.Name;
            calendarType.Description = request.Description;
            calendarType.IsActive = request.IsActive;

            var result = await _uow.CalendarTypes.UpdateAsync(calendarType);

            if (result == null)
                return null;

            return MapToResponse(result);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            return await _uow.CalendarTypes.DeleteAsync(id);
        }

        private static CalendarTypeResponse MapToResponse(
            CalendarType calendarType)
        {
            return new CalendarTypeResponse
            {
                CalendarTypeId = calendarType.CalendarTypeId,
                Name = calendarType.Name,
                Description = calendarType.Description,
                IsActive = calendarType.IsActive,
                CreatedAt = calendarType.CreatedAt
            };
        }
    }
}