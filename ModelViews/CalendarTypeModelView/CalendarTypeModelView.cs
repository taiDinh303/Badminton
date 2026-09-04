namespace BadmintonBooking.ModelViews.CalendarTypeModelViews;

public class CalendarTypeModelView
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }
}