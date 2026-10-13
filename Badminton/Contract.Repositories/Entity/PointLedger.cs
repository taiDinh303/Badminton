namespace Contract.Repositories.Entity
{
    public class PointLedger
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }

        /// <summary>Positive = earn, negative = clawback on refund.</summary>
        public int Delta { get; set; }

        public string Reason { get; set; } = null!;
        public int? BookingId { get; set; }
        public Booking? Booking { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
