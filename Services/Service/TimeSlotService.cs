using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Contract.Services.Interface;

namespace Services.Service
{
    public class TimeSlotService : ITimeSlotService
    {
        private readonly IUOW _uow;

        public TimeSlotService(IUOW uow)
        {
            _uow = uow;
        }

        public async Task<List<TimeSlot>> GetAllAsync()
        {
            return await _uow.TimeSlots.GetAllAsync();
        }

        public async Task<TimeSlot?> GetByIdAsync(int timeSlotId)
        {
            return await _uow.TimeSlots.GetByIdAsync(timeSlotId);
        }
    }
}