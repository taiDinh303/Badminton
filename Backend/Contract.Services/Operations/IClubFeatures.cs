using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IPlayerMatchingService
{
    Task<IReadOnlyList<PlayerSessionView>> SearchAsync(int? branchId, string? area, int? level, DateTime fromUtc, DateTime toUtc, int page);
    Task<int> CreateAsync(PlayerSessionRequest request);
    Task RequestAsync(int id);
    Task<IReadOnlyList<SessionMemberView>> MembersAsync(int id);
    Task DecideAsync(int id, int userId, bool accept);
    Task LeaveAsync(int id);
    Task CloseAsync(int id);
}
public interface IGroupExpenseService
{
    Task<IReadOnlyList<ExpenseBookingView>> BookingsAsync(int groupId, int page);
    Task<ExpenseView?> GetAsync(int bookingId);
    Task CreateAsync(int bookingId, int[] members);
    Task CreateByAttendanceAsync(int bookingId, AttendanceSplitRequest request);
    Task RecordAsync(int bookingId, int userId, bool paid);
}
public interface IEquipmentService
{
    Task<IReadOnlyList<EquipmentItemView>> ItemsAsync(int branchId);
    Task<int> SaveItemAsync(int? id, EquipmentItemRequest request);
    Task<int> ReserveAsync(EquipmentOrderRequest request);
    Task<IReadOnlyList<EquipmentOrderView>> OrdersAsync(bool staff, int? branchId, int page);
    Task TransitionAsync(int id, string action);
    Task RecordConditionAsync(int id, EquipmentConditionRequest request);
    Task<EquipmentConditionView?> ConditionAsync(int id);
}
public interface ICoachingService
{
    Task<IReadOnlyList<CoachView>> CoachesAsync(int branchId);
    Task<int> SaveCoachAsync(int? id, CoachRequest request);
    Task<IReadOnlyList<CoachSlotView>> SlotsAsync(int coachId, DateTime fromUtc, DateTime toUtc);
    Task<int> AddSlotAsync(int coachId, CoachSlotRequest request);
    Task CancelSlotAsync(int id);
    Task AttachCourtAsync(int id, int bookingId);
    Task<IReadOnlyList<CoachCourtChoice>> CourtChoicesAsync(int branchId);
    Task<int> ReserveAsync(CoachingRequest request);
    Task<IReadOnlyList<CoachingView>> ReservationsAsync(bool staff, int? branchId, int page);
    Task TransitionAsync(int id, string action);
}
public interface ITournamentService
{
    Task<IReadOnlyList<TournamentView>> ListAsync(int? branchId, int page);
    Task<int> CreateAsync(TournamentRequest request);
    Task<TournamentDetail> DetailAsync(int id);
    Task RegisterAsync(int id, TeamRequest request);
    Task WithdrawAsync(int id);
    Task DrawAsync(int id, int pools);
    Task ScheduleAsync(int id, int matchId, MatchScheduleRequest request);
    Task ScoreAsync(int id, int matchId, MatchScoreRequest request);
    Task<IReadOnlyList<ScoreRevisionView>> ScoreHistoryAsync(int id, int matchId);
    Task CancelAsync(int id);
}
public interface IClubMaintenanceService
{
    Task<int> RunAsync();
}
public interface ICoachCourtService
{
    Task<bool> ReadyAsync(int? bookingId, DateTime startsAtUtc, DateTime endsAtUtc, int capacity, int branchId);
    Task AttachAsync(int slotId, int bookingId);
    Task<IReadOnlyList<CoachCourtChoice>> ChoicesAsync(int branchId);
    Task<int> ReconcileAsync();
}
public interface ICalendarSubscriptionService
{
    Task<SubscriptionState> StateAsync();
    Task<SubscriptionToken> RotateAsync();
    Task RevokeAsync();
    Task<string> FeedAsync(string token);
}
