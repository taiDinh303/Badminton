using Badminton.Core.Enums;

namespace Contract.Repositories.Entity;

public sealed class OnlineRefundOperation
{
    public int Id { get; set; }
    public int PaymentId { get; set; }
    public int BookingId { get; set; }
    public string RequestId { get; set; } = null!;
    public int Amount { get; set; }
    public string Reason { get; set; } = null!;
    public int ActorId { get; set; }
    public bool CancelBooking { get; set; }
    public bool ReleaseVoucher { get; set; }
    public bool IsLateReceipt { get; set; }
    public bool Succeeded { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
