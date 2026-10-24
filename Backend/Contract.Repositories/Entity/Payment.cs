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
        public string? GatewayRef { get; set; }
        public string? ProviderTransactionId { get; set; }
        public string? ProviderTransactionDate { get; set; }
        public string? GatewayUrl { get; set; }
        public string? ReturnUrl { get; set; }
        public bool ReconciliationRequired { get; set; }
        public string? ReconciliationReason { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public int RefundAmount { get; set; }
        public DateTime? RefundedAt { get; set; }
        public string? RawResponse { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
