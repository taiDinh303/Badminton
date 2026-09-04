using ModelViews.BookingDetailModelViews;
using System.ComponentModel.DataAnnotations;

namespace ModelViews.Booking;

public class CreateBookingModelView
{
    public DateTime BookingDate { get; set; }
    public DateTime? BookingDeadline { get; set; }

    public decimal Price { get; set; }
    public string Status { get; set; } = "Pending";
    public bool PaymentStatus { get; set; }

    public Guid? UserId { get; set; }

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    public string? BankAccountID { get; set; }

    public ICollection<CreateBookingDetailModelView> BookingDetails { get; set; } = new List<CreateBookingDetailModelView>();
}