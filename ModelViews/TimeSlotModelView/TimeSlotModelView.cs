namespace BadmintonBooking.ModelViews.TimeSlotModelViews;

public class TimeSlotModelView
{
    public Guid Id { get; set; }

    public Guid CalendarTypeId { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public int DurationMinutes { get; set; }

    public bool IsActive { get; set; }
}