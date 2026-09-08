using Core.Base;
using System.Text.Json.Serialization;

namespace Contract.Repositories.Entity
{
    public class Booking : BaseEntity
    {
        public DateTime BookingDate { get; set; }                       // Ngày đặt lịch
        public DateTime? BookingDeadline { get; set; }                   // Hạn chót đặt lịch
        public decimal Price { get; set; }                             // Tổng tiền
        public bool PaymentStatus { get; set; }                        // Tình trạng thanh toán
        public string UserInfoId { get; set; } = string.Empty;          // ID thông tin người đặt
        public string UserName { get; set; } = string.Empty;            // Tên người đặt
        public string PhoneNumber { get; set; } = string.Empty;         // SĐT người đặt
        public string BankAccountID { get; set; } = string.Empty;       // ID tài khoản ngân hàng
        public string CalendarTypeID { get; set; } = string.Empty;      // ID loại lịch
        [JsonIgnore]
        public virtual UserInfo? UserInfo { get; set; }                 // Thông tin người đặt
        [JsonIgnore]
        public virtual BankAccount? BankAccount { get; set; }           // Thông tin tài khoản ngân hàng
        [JsonIgnore]
        public virtual CalendarType? CalendarType { get; set; }         // thông tin loại lịch
    }
}
