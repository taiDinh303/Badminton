using Badminton.Core.Enums;

namespace Contract.Repositories.Entity
{
    public class CourtDateStatusEntry
    {
        public int CourtId { get; set; }
        public Court? Court { get; set; }
        public DateOnly Date { get; set; }
        public CourtDateStatus Status { get; set; } = CourtDateStatus.Open;
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public bool Blocks(TimeOnly start, TimeOnly end) => Status != CourtDateStatus.Open
            && (!StartTime.HasValue || (start.ToTimeSpan() < EndTime!.Value && end.ToTimeSpan() > StartTime.Value));
        public string? Note { get; set; }
        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
