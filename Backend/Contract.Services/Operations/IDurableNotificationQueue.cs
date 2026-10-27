using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IDurableNotificationQueue
{
    Task QueueAsync(Badminton.Core.Enums.NotifyEvent evt, int userId, int? bookingId, string body, string eventKey, string[] channels);
    Task DeliverOneAsync(int id);
}
