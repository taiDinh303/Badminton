using System.ComponentModel.DataAnnotations;

namespace BadmintonBooking.ModelViews.BookingDetailModelViews;

public class UpdateBookingDetailModelView
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid BookingId { get; set; }

    [Required]
    public Guid CourtId { get; set; }

    [Required]
    public Guid TimeSlotId { get; set; }

    [Required]
    public DateTime PlayDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public string Status { get; set; } = "Pending";

    public string? Note { get; set; }
}