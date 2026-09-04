using System.ComponentModel.DataAnnotations;

namespace ModelViews.BranchModelViews;

public class UpdateBranchModelView
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public TimeSpan OpeningTime { get; set; }

    [Required]
    public TimeSpan ClosingTime { get; set; }

    public bool IsActive { get; set; } = true;
}