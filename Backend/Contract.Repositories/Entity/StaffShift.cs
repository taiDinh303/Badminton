using Badminton.Core.Base;
namespace Contract.Repositories.Entity;

public sealed class StaffShift : IntBaseEntity
{
    public int BranchId
    {
        get; set;
    }
    public int StaffId
    {
        get; set;
    }
    public DateTime StartsAtUtc
    {
        get; set;
    }
    public DateTime EndsAtUtc
    {
        get; set;
    }
    public string Note { get; set; } = "";
}
public sealed class HandoverItem : IntBaseEntity
{
    public int BranchId
    {
        get; set;
    }
    public int CreatedBy
    {
        get; set;
    }
    public string Text { get; set; } = "";
    public int? ClosedBy
    {
        get; set;
    }
    public DateTime? ClosedAt
    {
        get; set;
    }
    public string? Resolution
    {
        get; set;
    }
}
