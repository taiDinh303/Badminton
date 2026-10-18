using Badminton.Core.Base;
using Badminton.Core.Enums;

namespace Contract.Repositories.Entity
{
    public class WaitlistEntry : IntBaseEntity
    {
        public int UserId { get; set; }
        public User? User { get; set; }
        public int CourtId { get; set; }
        public Court? Court { get; set; }
        public int SlotId { get; set; }
        public TimeSlot? Slot { get; set; }
        public int BranchId { get; set; }
        public DateOnly Date { get; set; }
        public int PlayerCount { get; set; } = 1;
        public string? Note { get; set; }
        public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;
        public int? PromotedBookingId { get; set; }
        public string? DeclineReason { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }
}
