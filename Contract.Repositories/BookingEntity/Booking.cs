using BadmintonBooking.Core.Base;
using System.Text.Json.Serialization;

namespace Contract.Repositories.BookingEntity;

public class Booking : BaseEntity
{
    public DateTime BookingDate { get; set; }
    public DateTime? BookingDeadline { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Pending";
    public bool PaymentStatus { get; set; }

    public string UserInfoId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string BankAccountID { get; set; } = string.Empty;

    [JsonIgnore] public virtual UserInfo? UserInfo { get; set; }
    [JsonIgnore] public virtual BankAccount? BankAccount { get; set; }
    public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}