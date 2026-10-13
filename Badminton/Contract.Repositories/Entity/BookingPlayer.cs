namespace Contract.Repositories.Entity
{
    public class BookingPlayer
    {
        public int BookingId { get; set; }
        public Booking? Booking { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public bool IsCreator { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
