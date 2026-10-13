using M.Core.Base;
using M.Core.Enums;

namespace Contract.Repositories.Entity
{
    public class Booking : IntBaseEntity
    {
        /// <summary>Deterministic code, e.g. "BD-261013-XXXXXX".</summary>
        public string BookingCode { get; set; } = null!;

        /// the same key never duplicates the booking.</summary>
        public string? IdempotencyKey { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }
        public int? GroupId { get; set; }
        public PlayGroup? Group { get; set; }

        public int CourtId { get; set; }
        public Court? Court { get; set; }
        public int SlotId { get; set; }
        public TimeSlot? Slot { get; set; }
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        public DateOnly Date { get; set; }
        public int PlayerCount { get; set; } = 1;

        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        /// <summary>Pricing snapshot (VND, integer).</summary>
        public int TotalAmount { get; set; }
        public int DiscountAmount { get; set; }
        public int PayableAmount { get; set; }

        /// <summary>V1: 1 dong = 1 point on a completed booking.</summary>
        public int PointsEarned { get; set; }

        /// <summary>Pending bookings expire after 10 minutes.</summary>
        public DateTime? PendingExpiresAt { get; set; }

        public string? Note { get; set; }

        public string? CancelReason { get; set; }
        public DateTime? CancelledAt { get; set; }

        public int? VoucherRedemptionId { get; set; }

        public ICollection<BookingPlayer> Players { get; set; } = new List<BookingPlayer>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
