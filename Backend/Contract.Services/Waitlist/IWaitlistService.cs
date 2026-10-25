using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Badminton.Core.Base;
using Badminton.Core.Enums;

namespace Contract.Services.Waitlist
{
    public interface IWaitlistService
    {
        Task<WaitlistEntryView> JoinAsync(int userId, int courtId, int slotId,
            DateOnly date, int playerCount, string? note);
        Task<WaitlistEntryView> LeaveAsync(int entryId, int userId);
        Task<IReadOnlyList<WaitlistEntryView>> GetMineAsync(int userId);
        Task<int> ExpirePastEntriesAsync();
        Task<(IReadOnlyList<WaitlistEntryView> Items, int Total)> GetMinePagedAsync(int userId, Page page);
        Task<Contract.Repositories.Entity.Booking?> PromoteAsync(
            int courtId, int slotId, DateOnly date, int? freeReason = null);
    }

    public sealed record WaitlistEntryView(
        int Id, int CourtId, string CourtName, string SlotLabel, DateOnly Date,
        int PlayerCount, WaitlistStatus Status, DateTime CreatedAt,
        int? PromotedBookingId, string? DeclineReason = null);
}
