using Core.Base;
using System.ComponentModel.DataAnnotations;

namespace Contract.Repositories.Entity
{
    public class TimeFrame : BaseEntity
    {
        // Khóa ngoại liên kết với bảng đặt lịch
        public required string BookingID { get; set; }

        // Khóa ngoại liên kết với bảng sân
        public required string CourtID { get; set; }

        // Thời gian bắt đầu 
        public int StartTime { get; set; }

        // Thời gian kết thúc
        public int EndTime { get; set; }

        // Thứ trong tuần
        [StringLength(10)]
        public string DayOfWeek { get; set; } = string.Empty;

        public virtual Booking? Booking { get; set; }
        public virtual Court? Court { get; set; }
        public virtual ICollection<CheckIn> CheckIns { get; set; } = new HashSet<CheckIn>();

    }
}
