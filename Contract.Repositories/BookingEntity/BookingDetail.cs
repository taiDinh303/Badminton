using BadmintonBooking.Core.Base;

namespace Contract.Repositories.BookingEntity;

public class BookingDetail : BaseEntity
{
    public Guid BookingId { get; set; }
    public Guid CourtId { get; set; }
    public Guid TimeSlotId { get; set; }

    public DateTime PlayDate { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Note { get; set; }

    public virtual Booking? Booking { get; set; }
    public virtual Court? Court { get; set; }
    public virtual TimeSlot? TimeSlot { get; set; }
}