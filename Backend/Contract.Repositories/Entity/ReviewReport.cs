using Badminton.Core.Base;
namespace Contract.Repositories.Entity;

public sealed class ReviewReport : IntBaseEntity
{
    public int ReviewId
    {
        get; set;
    }
    public int UserId
    {
        get; set;
    }
    public string Reason { get; set; } = "";
    public CaseStatus Status
    {
        get; set;
    }
    public string? Resolution
    {
        get; set;
    }
    public int? ResolvedBy
    {
        get; set;
    }
}
public sealed class ReviewReply : IntBaseEntity
{
    public int ReviewId
    {
        get; set;
    }
    public int ActorId
    {
        get; set;
    }
    public string Text { get; set; } = "";
}
