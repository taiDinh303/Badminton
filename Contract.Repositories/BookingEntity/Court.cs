using BadmintonBooking.Core.Base;

namespace Contract.Repositories.BookingEntity;

public class Court : BaseEntity
{
    public Guid CourtTypeId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual CourtType? CourtType { get; set; }
    public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
}