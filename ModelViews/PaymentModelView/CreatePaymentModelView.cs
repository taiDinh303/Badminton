using System.ComponentModel.DataAnnotations;

namespace ModelViews.PaymentModelViews;

public class CreatePaymentModelView
{
    [Required]
    public Guid BookingId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    [Required]
    public string PaymentMethod { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public string? TransactionCode { get; set; }

    public DateTime? PaidAt { get; set; }
}