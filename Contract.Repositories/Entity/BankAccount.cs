using Core.Base;
using System.Text.Json.Serialization;

namespace Contract.Repositories.Entity
{
    public class BankAccount : BaseEntity
    {
        // Số tài khoản
        public string AccountNumber { get; set; }

        // Tên tài khoản
        public string AccountName { get; set; } = string.Empty;

        // Ngân hàng giao dịch
        public string Bank { get; set; }

        // Liên kết giữa bank account với nhiều booking
        [JsonIgnore]
        public virtual ICollection<Booking> Bookings { get; set; } = new HashSet<Booking>();
    }
}
