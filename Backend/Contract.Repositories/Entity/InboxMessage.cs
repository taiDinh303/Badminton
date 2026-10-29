using Badminton.Core.Base;
namespace Contract.Repositories.Entity;

public sealed class InboxMessage : IntBaseEntity
{
    public int UserId
    {
        get; set;
    }
    public int? BookingId
    {
        get; set;
    }
    public string EventKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime? ReadAt
    {
        get; set;
    }
}
public sealed class NotificationPreference : IntBaseEntity
{
    public int UserId
    {
        get; set;
    }
    public bool RemindersEnabled { get; set; } = true;
    public int MinutesBefore { get; set; } = 60;
    public bool Email
    {
        get; set;
    }
    public bool Sms
    {
        get; set;
    }
    public bool Zalo
    {
        get; set;
    }
}
