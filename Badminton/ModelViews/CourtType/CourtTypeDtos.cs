namespace ModelViews.CourtType
{
    // ---------- Court type (master) ----------
    public record CourtTypeView(int Id, string Name, string? Description, bool IsActive);

    public record CreateCourtTypeRequest(string Name, string? Description);

    public record UpdateCourtTypeRequest(string? Name, string? Description, bool? IsActive);
}
