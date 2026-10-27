namespace Contract.Repositories.Entity
{
    public class Review
    {
        public bool IsHidden
        {
            get; set;
        }
        public string? ModerationReason
        {
            get; set;
        }
        public int Id
        {
            get; set;
        }
        public int BookingId
        {
            get; set;
        }
        public Booking? Booking
        {
            get; set;
        }
        public int CourtId
        {
            get; set;
        }
        public Court? Court
        {
            get; set;
        }
        public int UserId
        {
            get; set;
        }
        public User? User
        {
            get; set;
        }
        public int Rating
        {
            get; set;
        }
        public string? Comment
        {
            get; set;
        }
        public string? Images
        {
            get; set;
        }
        public bool FlaggedRefunded
        {
            get; set;
        }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
