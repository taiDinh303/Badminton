using Badminton.Core.Base;
namespace Contract.Repositories.Entity;

public sealed class StoredMedia : IntBaseEntity
{
    public int OwnerId
    {
        get; set;
    }
    public string Key { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Length
    {
        get; set;
    }
    public int? CourtId
    {
        get; set;
    }
    public int? ReviewId
    {
        get; set;
    }
    public bool IsPublic
    {
        get; set;
    }
}
