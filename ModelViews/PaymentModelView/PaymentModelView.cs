namespace BadmintonBooking.ModelViews.PaymentModelViews;

public class PaymentModelView
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? TransactionCode { get; set; }

    public DateTime? PaidAt { get; set; }
}