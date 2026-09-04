namespace ModelViews.BookingDetailModelViews;

public class BookingDetailModelView
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public Guid CourtId { get; set; }

    public Guid TimeSlotId { get; set; }

    public DateTime PlayDate { get; set; }

    public decimal Price { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }
}