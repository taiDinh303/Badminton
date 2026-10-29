using Badminton.Core.Base;
namespace Contract.Repositories.Entity;

public sealed class BookingBatch : IntBaseEntity
{
    public int ActorId
    {
        get; set;
    }
    public string RequestKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string BookingIdsJson { get; set; } = "[]";
}
