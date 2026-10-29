using Badminton.Core.Base;
namespace Contract.Repositories.Entity;

public sealed class BookingHistory : IntBaseEntity
{
    public int BookingId
    {
        get; set;
    }
    public int ActorId
    {
        get; set;
    }
    public string Event { get; set; } = "";
    public string Detail { get; set; } = "";
}
