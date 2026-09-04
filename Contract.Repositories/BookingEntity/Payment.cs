using BadmintonBooking.Core.Base;

namespace Contract.Repositories.BookingEntity;

public class Payment : BaseEntity
{
    public Guid BookingId { get; set; }

    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? TransactionCode { get; set; }
    public DateTime? PaidAt { get; set; }

    public virtual Booking? Booking { get; set; }
}