using Badminton.Core.Base;
namespace Contract.Repositories.Entity;

public sealed class InventoryDaySnapshot : IntBaseEntity
{
    public int BranchId { get; set; }
    public DateOnly Date { get; set; }
    public string CapacityJson { get; set; } = "[]";
}
