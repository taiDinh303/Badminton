namespace Contract.Repositories.AuthEntity
{
    public class TimeSlot
    {
        public int TimeSlotId { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public bool IsActive { get; set; }

        public ICollection<BookingDetail> BookingDetails { get; set; }
            = new List<BookingDetail>();
    }
}