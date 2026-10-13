using M.Core.Base;
using M.Core.Enums;

namespace Contract.Repositories.Entity
{
    /// <summary>
    /// 1-1 payment for a booking. INHERITANCE: extends
    /// <see cref="IntBaseEntity"/>.
    /// </summary>
    public class Payment : IntBaseEntity
    {
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }

        public PaymentMethod Method { get; set; }
        public string? TransactionCode { get; set; }
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public DateTime? PaidAt { get; set; }
    }
}
