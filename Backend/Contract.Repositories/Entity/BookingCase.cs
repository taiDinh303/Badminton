using Badminton.Core.Base;
namespace Contract.Repositories.Entity;

public enum CaseStatus
{
    Open, Accepted, Rejected
}
public enum BookingCaseKind
{
    NoShow, Attendance, Other
}
public sealed class BookingCase : IntBaseEntity
{
    public int BookingId
    {
        get; set;
    }
    public int UserId
    {
        get; set;
    }
    public BookingCaseKind Kind
    {
        get; set;
    }
    public string Reason { get; set; } = "";
    public CaseStatus Status
    {
        get; private set;
    }
    public string? Resolution
    {
        get; private set;
    }
    public int? ResolvedBy
    {
        get; private set;
    }
    public void Resolve(bool accepted, string resolution, int actor)
    {
        if (Status != CaseStatus.Open)
            throw new ConflictException("Yêu cầu đã được xử lý.");
        Status = accepted ? CaseStatus.Accepted : CaseStatus.Rejected;
        Resolution = resolution;
        ResolvedBy = actor;
        UpdatedAt = DateTime.UtcNow;
    }
}
