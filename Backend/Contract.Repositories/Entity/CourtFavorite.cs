using Badminton.Core.Base;
namespace Contract.Repositories.Entity;

public sealed class CourtFavorite : IntBaseEntity
{
    public int UserId
    {
        get; set;
    }
    public int CourtId
    {
        get; set;
    }
}
public sealed class GroupRsvp : IntBaseEntity
{
    public int BookingId
    {
        get; set;
    }
    public int UserId
    {
        get; set;
    }
    public bool? Attending
    {
        get; set;
    }
}
