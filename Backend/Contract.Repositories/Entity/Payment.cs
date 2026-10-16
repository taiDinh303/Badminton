using Badminton.Core.Enums;

namespace Contract.Repositories.Entity
{
    public class Payment
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }

        public int Amount { get; set; }
        public PaymentMethod Method { get; set; }

        /// <summary>Gateway transaction reference (webhook idempotency key).</summary>
        public string? GatewayRef { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public int RefundAmount { get; set; }
        public DateTime? RefundedAt { get; set; }
        /// <summary>Full gateway response, JSON.</summary>
        public string? RawResponse { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
