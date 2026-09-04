using System.ComponentModel.DataAnnotations;

namespace BadmintonBooking.ModelViews.CalendarTypeModelViews;

public class CreateCalendarTypeModelView
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}