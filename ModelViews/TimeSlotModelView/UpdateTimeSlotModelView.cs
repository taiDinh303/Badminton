using System.ComponentModel.DataAnnotations;

namespace ModelViews.TimeSlotModelViews;

public class UpdateTimeSlotModelView
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid CalendarTypeId { get; set; }

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Range(1, int.MaxValue)]
    public int DurationMinutes { get; set; }

    public bool IsActive { get; set; } = true;
}