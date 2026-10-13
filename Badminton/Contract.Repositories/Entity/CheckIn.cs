using M.Core.Base;
using M.Core.Enums;

namespace Contract.Repositories.Entity
{
    /// <summary>
    /// Track a customer's physical check-in/out for a booking.
    /// INHERITANCE: extends <see cref="IntBaseEntity"/>.
    /// </summary>
    public class CheckIn : IntBaseEntity
    {
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public CheckInStatus Status { get; set; } = CheckInStatus.Pending;
        public string? Note { get; set; }
    }
}
