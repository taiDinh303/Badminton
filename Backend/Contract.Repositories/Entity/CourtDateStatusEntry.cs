using Badminton.Core.Enums;

namespace Contract.Repositories.Entity
{
    public class CourtDateStatusEntry
    {
        public int CourtId { get; set; }
        public Court? Court { get; set; }
        public DateOnly Date { get; set; }
        public CourtDateStatus Status { get; set; } = CourtDateStatus.Open;
        public string? Note { get; set; }
        public int? CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
