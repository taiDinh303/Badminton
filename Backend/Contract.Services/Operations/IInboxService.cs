using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IInboxService
{
    Task<int> UnreadCountAsync();
    Task<IReadOnlyList<InboxView>> ListAsync(int page);
    Task ReadAsync(int id);
    Task<PreferenceView> PreferencesAsync();
    Task SetPreferencesAsync(PreferenceView request);
    Task PublishAsync(int userId, int? bookingId, string eventKey, string title, string body);
}
