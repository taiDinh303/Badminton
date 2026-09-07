namespace ModelViews.Court
{
    public class CourtResponse
    {
        public int CourtId { get; set; }
        public int CourtTypeId { get; set; }
        public string CourtCode { get; set; } = string.Empty;
        public string CourtName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Location { get; set; }
        public decimal PricePerHour { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
    }
}