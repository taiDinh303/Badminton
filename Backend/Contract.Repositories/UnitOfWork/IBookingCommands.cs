using Badminton.Core.Enums;

namespace Contract.Repositories.UnitOfWork;

public interface IBookingCommands
{
    Task<int> ConfirmAsync(int id, string qrToken, DateTime now);
    Task<int> CompleteAsync(int id, int points, DateTime now, DateTime? businessNow = null);
    Task<int> CancelPendingAsync(int id, string reason, DateTime now);
    Task<int> MarkNoShowAsync(int id, string reason, DateTime now, DateTime? businessNow = null);
    Task<int> ClaimRefundAsync(int id, bool cancel, string reason, DateTime now);
    Task<int> CheckinAsync(int id, DateTime now, DateTime? businessNow = null);
}
