using Contract.Repositories.Entity;

namespace Contract.Repositories.IOUW
{
    public interface ITimeSlotRepository
    {
        Task<List<TimeSlot>> GetAllAsync();

        Task<TimeSlot?> GetByIdAsync(int timeSlotId);
    }
}