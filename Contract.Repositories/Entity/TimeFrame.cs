using Core.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Contract.Repositories.Entity
{
    /// <summary>
    /// Khung giờ khả dụng của một sân.
    /// Một sân (Court) có nhiều TimeFrame; một TimeFrame có thể gắn với
    /// một bản đặt (Booking) khi đã được chiếm – hoặc null khi còn trống.
    /// </summary>
    [Table("TimeFrame")]
    public class TimeFrame : BaseEntity
    {
        /// <summary>Khóa ngoại liên kết với bảng sân.</summary>
        public Guid CourtId { get; set; }

        /// <summary>
        /// Khóa ngoại liên kết với bản đặt (null = khung giờ còn trống).
        /// </summary>
        public Guid? BookingId { get; set; }

        /// <summary>Giờ bắt đầu (0-23).</summary>
        public int StartTime { get; set; }

        /// <summary>Giờ kết thúc (0-23).</summary>
        public int EndTime { get; set; }

        /// <summary>Thứ trong tuần áp dụng ("All", "Mon", …).</summary>
        [StringLength(10)]
        public string DayOfWeek { get; set; } = "All";

        /// <summary>Ngày cụ thể của bản đặt (null = khung giờ mở định kỳ).</summary>
        public DateTime? SlotDate { get; set; }

        // ======== Quan hệ ========
        [JsonIgnore]
        public virtual Court? Court { get; set; }

        [JsonIgnore]
        public virtual Booking? Booking { get; set; }

        [JsonIgnore]
        public virtual ICollection<CheckIn> CheckIns { get; set; } = new HashSet<CheckIn>();

        /// <summary>Khung giờ đã bị đặt hay chưa.</summary>
        public bool IsBooked => BookingId.HasValue;
    }
}
