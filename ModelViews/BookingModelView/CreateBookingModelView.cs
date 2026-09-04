using System.ComponentModel.DataAnnotations;

namespace BadmintonBooking.ModelViews.BookingModelViews;

public class CreateBookingModelView
{
    [Required]
    public DateTime BookingDate { get; set; }

    public DateTime? BookingDeadline { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public string Status { get; set; } = "Pending";

    public bool PaymentStatus { get; set; }

    [Required]
    public string UserInfoId { get; set; } = string.Empty;

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    public string BankAccountID { get; set; } = string.Empty;
}