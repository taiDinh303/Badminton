using M.Core.Base;
using M.Core.Enums;

namespace Contract.Repositories.Entity
{
    /// <summary>
    /// A customer's reservation of one or more court slots on a date.
    /// INHERITANCE: extends <see cref="IntBaseEntity"/>.
    /// </summary>
    public class Booking : IntBaseEntity
    {
        public string BookingCode { get; set; } = null!;
        public int UserId { get; set; }
        public User? User { get; set; }

        public DateOnly BookingDate { get; set; }
        public decimal TotalAmount { get; set; }
        public BookingStatus Status { get; set; } = BookingStatus.Pending;
        public string? Note { get; set; }

        /// <summary>Which fee policy produced <see cref="TotalAmount"/> (audit).</summary>
        public string? AppliedFeePolicy { get; set; }

        public ICollection<BookingDetail> Details { get; set; } = new List<BookingDetail>();
        public Payment? Payment { get; set; }

        /// <summary>Deterministic, human-readable booking code.</summary>
        public static string GenerateCode(DateTime utcNow) =>
            "BD-" + utcNow.ToString("yyMMddHHmmss") + "-" +
            Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }
}
