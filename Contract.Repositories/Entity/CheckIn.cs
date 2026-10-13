using Core.Base;
using System.Text.Json.Serialization;

namespace Contract.Repositories.Entity
{
    /// <summary>
    /// Ghi nhận khách đã đến sân (check-in) cho một bản đặt.
    /// Một booking có thể có nhiều lượt check-in (khách mời theo lượt).
    /// </summary>
    [Table("CheckIn")]
    public class CheckIn : BaseEntity
    {
        /// <summary>Thời điểm khách vào.</summary>
        public DateTime CheckInDate { get; set; }

        /// <summary>Khóa ngoại liên kết với bản đặt.</summary>
        public Guid BookingId { get; set; }

        [JsonIgnore]
        public virtual Booking? Booking { get; set; }
    }
}
