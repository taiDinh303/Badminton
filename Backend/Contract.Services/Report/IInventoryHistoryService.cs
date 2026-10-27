namespace Contract.Services.Report;

public sealed record InventorySlot(int Court, int Slot, bool Peak);

public interface IInventoryHistoryService
{
    Task CaptureTodayAsync(int? branchId = null);
    Task<IReadOnlyList<InventorySlot>> CurrentCapacityAsync(int branchId, DateOnly day);
}
