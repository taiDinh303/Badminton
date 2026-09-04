namespace ModelViews.CourtModelViews;

public class CourtModelView
{
    public Guid Id { get; set; }

    public Guid CourtTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }
}