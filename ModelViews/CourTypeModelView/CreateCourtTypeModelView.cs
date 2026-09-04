using System.ComponentModel.DataAnnotations;

namespace ModelViews.CourtTypeModelViews;

public class CreateCourtTypeModelView
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}