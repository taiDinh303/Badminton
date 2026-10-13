using M.Core.Base;
using M.Core.Enums;

namespace Contract.Repositories.Entity
{
    /// <summary>
    /// One court slot within a booking. Stores its own copy of the booking
    /// date so SQL can enforce "a slot is reserved once" via a unique index.
    /// INHERITANCE: extends <see cref="IntBaseEntity"/>.
    /// </summary>
    public class BookingDetail : IntBaseEntity
    {
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }

        public int CourtId { get; set; }
        public Court? Court { get; set; }

        public int TimeSlotId { get; set; }
        public TimeSlot? TimeSlot { get; set; }

        public DateOnly BookingDate { get; set; }

        /// <summary>Price snapshot for this slot at booking time (audit).</summary>
        public decimal Price { get; set; }
        public BookingDetailStatus Status { get; set; } = BookingDetailStatus.Reserved;
    }
}
