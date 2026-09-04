using System.ComponentModel.DataAnnotations;

namespace BadmintonBooking.ModelViews.CourtModelViews;

public class CreateCourtModelView
{
    [Required]
    public Guid CourtTypeId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}