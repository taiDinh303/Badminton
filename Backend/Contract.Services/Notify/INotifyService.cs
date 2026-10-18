using Contract.Repositories.Entity;
using Badminton.Core.Enums;

namespace Contract.Services.Notify
{
    public interface INotifyChannel
    {
        string Name { get; }
        bool IsConfigured { get; }
        Task SendAsync(User user, NotifyEvent evt, string body);
    }

    public interface INotifyService
    {
        Task<IReadOnlyList<Notification>> EnqueueAndSendAsync(NotifyEvent evt,
            int userId, int? bookingId, string body);

        Task NotifyBookingConfirmedAsync(int bookingId);
        Task NotifyBookingExpiredAsync(int bookingId);
        Task NotifyNoShowAsync(int bookingId);
        Task NotifyWaitlistPromotedAsync(int waitlistEntryId, int bookingId);
    }
}
