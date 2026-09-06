namespace Contract.Repositories.Entity
{
    public class Court
    {
        public int CourtId { get; set; }

        public int CourtTypeId { get; set; }

        public string CourtCode { get; set; } = string.Empty;

        public string CourtName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string? Location { get; set; }

        public decimal PricePerHour { get; set; }

        public string Status { get; set; } = "Available";

        public string? ImageUrl { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public ICollection<BookingDetail> BookingDetails { get; set; }
            = new List<BookingDetail>();
    }
}