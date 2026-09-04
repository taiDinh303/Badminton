using System.ComponentModel.DataAnnotations;

namespace BadmintonBooking.ModelViews.CourtTypeModelViews;

public class UpdateCourtTypeModelView
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}