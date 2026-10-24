using Badminton.Core.Base;
using Badminton.Core.Enums;

namespace Contract.Repositories.Entity
{
    public class Booking : IntBaseEntity
    {
        public void Complete(int earnedPoints, DateTime now)
        {
            if (Status != BookingStatus.Confirmed)
                throw new Badminton.Core.Base.ConflictException("Chỉ hoàn thành đơn đã xác nhận.");
            if (earnedPoints < 0) throw new ArgumentOutOfRangeException(nameof(earnedPoints));
            Status = BookingStatus.Completed;
            PointsEarned = earnedPoints;
            UpdatedAt = now;
        }
        public string BookingCode { get; set; } = null!;
        public string? IdempotencyKey { get; set; }
        public string? RequestFingerprint { get; set; }
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
        public int TotalAmount { get; set; }
        public int DiscountAmount { get; set; }
        public int PayableAmount { get; set; }
        public int PointsEarned { get; set; }
        public int PointsRedeemed { get; set; }
        public DateTime? PendingExpiresAt { get; set; }
        public string? Note { get; set; }
        public string? CancelReason { get; set; }
        public DateTime? CancelledAt { get; set; }
        public int? VoucherRedemptionId { get; set; }
        public string? QrToken { get; set; }
        public DateTime? CheckedInAt { get; set; }
        public ICollection<BookingPlayer> Players { get; set; } = new List<BookingPlayer>();
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
