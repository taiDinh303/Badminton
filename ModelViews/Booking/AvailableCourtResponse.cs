namespace ModelViews.Booking
{
    public class AvailableCourtResponse
    {
        public int CourtId { get; set; }
        public string CourtCode { get; set; } = string.Empty;
        public string CourtName { get; set; } = string.Empty;
        public decimal PricePerHour { get; set; }
        public List<AvailableTimeSlotResponse> TimeSlots { get; set; } = new();
    }

    public class AvailableTimeSlotResponse
    {
        public int TimeSlotId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsAvailable { get; set; }
    }
}