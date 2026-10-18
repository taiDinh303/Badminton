namespace ModelViews.Court
{
    public record CourtView(int Id, int BranchId, string Code, string Name,
        int PriceLowPeak, int PricePeak, decimal WeekendSurcharge, int Capacity,
        string Surface, bool IsActive, double? RatingAvg, int RatingCount);

    public record CreateCourtRequest(int BranchId, string Code, string Name,
        int PriceLowPeak, int PricePeak, decimal WeekendSurcharge = 0.20m,
        int Capacity = 2, string Surface = "Sindalo", string? Amenities = null);

    public record UpdateCourtRequest(string? Name, int? PriceLowPeak, int? PricePeak,
        decimal? WeekendSurcharge, int? Capacity, string? Surface, string? Amenities,
        bool? IsActive);
}
