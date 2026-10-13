using ModelViews.TimeSlot;

namespace Contract.Services.TimeSlot
{
    public interface ITimeSlotService
    {
        Task<IReadOnlyList<TimeSlotView>> GetAllAsync();
        Task CreateAsync(CreateTimeSlotRequest request, string userName);
        Task UpdateAsync(int id, UpdateTimeSlotRequest request, string userName);
        Task DeleteAsync(int id, string userName);
    }
}
