using Core.Base;

namespace Contract.Repositories.BookingEntity;

public class TimeSlot : BaseEntity
{
    public Guid CalendarTypeId { get; set; }

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual CalendarType? CalendarType { get; set; }
    public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
}