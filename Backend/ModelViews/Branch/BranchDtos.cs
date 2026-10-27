namespace ModelViews.Branch
{
    public record BranchView(int Id, string Name, string Address, bool IsActive,
        string OpenTime, string CloseTime, int CourtCount,
        double? GeoLat = null, double? GeoLng = null);

    public record CreateBranchRequest(string Name, string Address, double? GeoLat,
        double? GeoLng, string OpenTime = "06:00", string CloseTime = "23:00");

    /// <summary>
    /// Set/clear position: khi <see cref="HasGeo"/>=true, áp dụng cặp GeoLat/GeoLng
    /// (cả hai null = xóa vị trí). Khi HasGeo=false (PATCH không gửi geo), giữ nguyên vị trí đã lưu.
    /// </summary>
    public record UpdateBranchRequest(string? Name, string? Address, bool? IsActive,
        string? OpenTime, string? CloseTime,
        double? GeoLat = null, double? GeoLng = null, bool HasGeo = false);
}
