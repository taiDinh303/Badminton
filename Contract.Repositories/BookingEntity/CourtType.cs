using Core.Base;

namespace Contract.Repositories.BookingEntity;

public class CourtType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Court> Courts { get; set; } = new List<Court>();
}