namespace ModelViews.Branch
{
    public record BranchView(int Id, string Name, string Address, bool IsActive,
        string OpenTime, string CloseTime, int CourtCount);

    public record CreateBranchRequest(string Name, string Address, double? GeoLat,
        double? GeoLng, string OpenTime = "06:00", string CloseTime = "23:00");

    public record UpdateBranchRequest(string? Name, string? Address, bool? IsActive,
        string? OpenTime, string? CloseTime);
}
