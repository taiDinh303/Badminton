using ModelViews.BookingDetailModelViews;

namespace ModelViews.Booking;

public class BookingModelView
{
    public Guid Id { get; set; }

    public DateTime BookingDate { get; set; }
    public DateTime? BookingDeadline { get; set; }

    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool PaymentStatus { get; set; }

    public Guid? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    public string? BankAccountID { get; set; }

    public ICollection<BookingDetailModelView> BookingDetails { get; set; } = new List<BookingDetailModelView>();
}