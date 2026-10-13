using M.Core.Enums;

namespace ModelViews.Court
{
    // ---------- Court ----------
    public record CourtView(int Id, string CourtCode, string CourtName, string? Location,
        decimal PricePerHour, CourtStatus Status, string CourtTypeName);

    public record CreateCourtRequest(int CourtTypeId, string CourtCode, string CourtName,
        string? Location, decimal PricePerHour, string? ImageUrl);

    public record UpdateCourtRequest(string? CourtName, string? Location,
        decimal? PricePerHour, CourtStatus? Status, string? ImageUrl);
}
