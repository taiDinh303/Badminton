using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IStaffWorkService
{
    Task<IReadOnlyList<ShiftStaffView>> StaffAsync(int? branchId);
    Task<IReadOnlyList<ShiftView>> ShiftsAsync(DateOnly from, DateOnly to, int? branchId);
    Task<int> CreateShiftAsync(ShiftRequest request);
    Task DeleteShiftAsync(int id);
    Task<IReadOnlyList<HandoverView>> HandoverAsync(int? branchId, int page);
    Task<int> CreateHandoverAsync(HandoverRequest request);
    Task CloseHandoverAsync(int id, ContactOutcomeRequest request);
}
