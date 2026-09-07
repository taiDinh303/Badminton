namespace ModelViews.Booking
{
    public class CreateBookingRequest
    {
        public DateOnly BookingDate { get; set; }
        public string? Note { get; set; }
        public List<CreateBookingDetailRequest> Details { get; set; } = new();
    }

    public class CreateBookingDetailRequest
    {
        public int CourtId { get; set; }
        public int TimeSlotId { get; set; }
    }
}