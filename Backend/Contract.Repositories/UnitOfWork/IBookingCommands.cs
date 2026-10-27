using Badminton.Core.Enums;

namespace Contract.Repositories.UnitOfWork;

public interface IBookingCommands
{
    Task<int> RescheduleAsync(int id, int oldCourtId, int oldSlotId, DateOnly oldDate,
        int courtId, int slotId, DateOnly date, int branchId, DateTime now, DateTime businessNow);
    Task<int> ConfirmAsync(int id, string qrToken, DateTime now, int? expectedBranchId = null);
    Task<int> CompleteAsync(int id, int points, DateTime now, DateTime? businessNow = null);
    Task<int> CancelPendingAsync(int id, string reason, DateTime now, int? expectedBranchId = null);
    Task<int> MarkNoShowAsync(int id, string reason, DateTime now, DateTime? businessNow = null, int? expectedBranchId = null);
    Task<int> ClaimRefundAsync(int id, bool cancel, string reason, DateTime now, int? expectedBranchId = null);
    Task<int> CheckinAsync(int id, DateTime now, DateTime? businessNow = null, int? expectedBranchId = null);
}
