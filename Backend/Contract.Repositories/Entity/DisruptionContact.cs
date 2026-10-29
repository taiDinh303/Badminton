using Badminton.Core.Base;
namespace Contract.Repositories.Entity;

public sealed class DisruptionContact : IntBaseEntity
{
    public int BookingId
    {
        get; set;
    }
    public int BranchId
    {
        get; set;
    }
    public string IncidentKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Outcome
    {
        get; set;
    }
    public DateTime? ContactedAt
    {
        get; set;
    }
    public int? ContactedBy
    {
        get; set;
    }
}
