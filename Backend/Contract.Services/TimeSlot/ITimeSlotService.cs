using ModelViews.TimeSlot;

namespace Contract.Services.TimeSlot
{
    public interface ITimeSlotService
    {
        Task<IReadOnlyList<TimeSlotView>> GetAllAsync(bool includeInactive = false);
        Task<int> CreateAsync(CreateTimeSlotRequest request, int actorId);
        Task UpdateAsync(int id, UpdateTimeSlotRequest request, int actorId);
        Task DeleteAsync(int id, int actorId);
    }
}
