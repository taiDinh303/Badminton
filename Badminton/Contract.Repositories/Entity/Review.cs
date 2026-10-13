using M.Core.Base;

namespace Contract.Repositories.Entity
{
    /// <summary>
    /// A customer's rating/comment on a court after a booking.
    /// INHERITANCE: extends <see cref="IntBaseEntity"/>.
    /// </summary>
    public class Review : IntBaseEntity
    {
        public int UserId { get; set; }
        public User? User { get; set; }

        public int CourtId { get; set; }
        public Court? Court { get; set; }

        public int BookingId { get; set; }
        public Booking? Booking { get; set; }

        /// <summary>1-5 rating.</summary>
        public byte Rating { get; set; }
        public string? Comment { get; set; }
    }
}
