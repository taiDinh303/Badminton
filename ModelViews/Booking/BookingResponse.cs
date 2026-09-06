using ModelViews.Payment;

namespace ModelViews.Booking
{
    public class BookingResponse
    {
        public int BookingId { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public int UserId { get; set; }
        public DateTime BookingDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }

        public List<BookingDetailResponse> Details { get; set; } = new();

        public PaymentResponse? Payment { get; set; }
    }

    public class BookingDetailResponse
    {
        public int BookingDetailId { get; set; }
        public int CourtId { get; set; }
        public string CourtName { get; set; } = string.Empty;

        public int TimeSlotId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public DateTime BookingDate { get; set; }

        public decimal Price { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}