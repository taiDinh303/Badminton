using Core.Base;

namespace Contract.Repositories.BookingEntity;

public class BankAccount : BaseEntity
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string? QRCodeUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}