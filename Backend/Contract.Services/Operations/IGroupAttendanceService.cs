using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IGroupAttendanceService
{
    Task<IReadOnlyList<GroupMeetingView>> MeetingsAsync(int groupId);
    Task<IReadOnlyList<RsvpView>> RsvpsAsync(int bookingId);
    Task InviteMembersAsync(int bookingId, int[] userIds);
    Task RespondAsync(int bookingId, bool attending);
}
