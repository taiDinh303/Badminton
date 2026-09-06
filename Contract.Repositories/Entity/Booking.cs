namespace Contract.Repositories.Entity
{
    public class Booking
    {
        public int BookingId { get; set; }

        public string BookingCode { get; set; } = string.Empty;

        public int UserId { get; set; }

        public DateTime BookingDate { get; set; }

        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = "Pending";

        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public User User { get; set; } = null!;

        public ICollection<BookingDetail> BookingDetails { get; set; }
            = new List<BookingDetail>();

        public Payment? Payment { get; set; }
    }
}