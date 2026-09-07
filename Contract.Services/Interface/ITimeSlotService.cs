using Contract.Repositories.AuthEntity;

namespace Contract.Services.Interface
{
    public interface ITimeSlotService
    {
        Task<List<TimeSlot>> GetAllAsync();

        Task<TimeSlot?> GetByIdAsync(int timeSlotId);
    }
}