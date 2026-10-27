using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using System.Text.Json;
using Contract.Services.Report;
namespace Services;


/// Captures today's latest known capacity; past rows are never rewritten using current configuration.
public sealed class InventoryHistoryService(IUnitOfWork uow) : IInventoryHistoryService
{
    public async Task CaptureTodayAsync(int? branchId = null)
    {
        var today = HanoiTime.HanoiToday();
        var branches = await uow.GetRepository<Branch>().GetByConditionAsync(b => branchId == null || b.Id == branchId);
        foreach (var branch in branches.OrderBy(b => b.Id))
            await uow.RunInTransactionAsync(async () => {
                await uow.LockBookingRequestAsync($"branch-config:{branch.Id}");
                var capacity = await CurrentCapacityAsync(branch.Id, today);
                var repo = uow.GetRepository<InventoryDaySnapshot>();
                var row = (await repo.GetByConditionAsync(x => x.BranchId == branch.Id && x.Date == today)).SingleOrDefault();
                var json = JsonSerializer.Serialize(capacity);
                if (row is null) await repo.AddAsync(new InventoryDaySnapshot { BranchId = branch.Id, Date = today, CapacityJson = json });
                else if (row.CapacityJson != json) { var tracked = (await repo.GetByIdAsync(row.Id))!; tracked.CapacityJson = json; tracked.UpdatedAt = DateTime.UtcNow; repo.UpdateProperties(tracked, nameof(InventoryDaySnapshot.CapacityJson), nameof(InventoryDaySnapshot.UpdatedAt)); }
                await uow.SaveChangesAsync();
            });
    }

    public async Task<IReadOnlyList<InventorySlot>> CurrentCapacityAsync(int branchId, DateOnly day)
    {
        var branch = (await uow.GetRepository<Branch>().GetByConditionAsync(b => b.Id == branchId)).Single();
        if (!branch.IsActive || branch.DeletedAt.HasValue || branch.Status != BranchStatus.Active) return [];
        var courts = await uow.GetRepository<Court>().GetByConditionAsync(c => c.BranchId == branchId && c.IsActive && c.DeletedAt == null);
        var ids = courts.Select(c => c.Id).ToList();
        var schedules = (await uow.GetRepository<CourtDateStatusEntry>().GetByConditionAsync(x => x.Date == day && ids.Contains(x.CourtId))).ToDictionary(x => x.CourtId);
        var slots = await uow.GetRepository<TimeSlot>().GetByConditionAsync(s => s.IsActive && s.DeletedAt == null && s.StartTime >= branch.OpenTime && s.EndTime <= branch.CloseTime);
        return courts.SelectMany(c => slots.Where(s => !schedules.TryGetValue(c.Id, out var schedule) || !schedule.Blocks(s.StartTime, s.EndTime))
            .Select(s => new InventorySlot(c.Id, s.Id, s.IsPeak))).ToList();
    }
}
