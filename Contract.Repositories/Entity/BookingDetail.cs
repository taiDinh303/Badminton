namespace Contract.Repositories.Entity
{
    public class BookingDetail
    {
        public int BookingDetailId { get; set; }

        public int BookingId { get; set; }

        public int CourtId { get; set; }

        public int TimeSlotId { get; set; }

        public DateTime BookingDate { get; set; }

        public decimal Price { get; set; }

        public string Status { get; set; } = "Reserved";

        public Booking Booking { get; set; } = null!;

        public Court Court { get; set; } = null!;

        public TimeSlot TimeSlot { get; set; } = null!;
    }
}