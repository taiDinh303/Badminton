namespace BadmintonBooking.ModelViews.BookingModelViews;

public class BookingModelView
{
    public string Id { get; set; } = string.Empty;

    public DateTime BookingDate { get; set; }

    public DateTime? BookingDeadline { get; set; }

    public decimal Price { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool PaymentStatus { get; set; }

    public string UserInfoId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string BankAccountID { get; set; } = string.Empty;
}