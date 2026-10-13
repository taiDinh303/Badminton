namespace ModelViews.TimeSlotModelView
{
    /// <summary>DTO khung giờ trả về cho client.</summary>
    public class TimeFrameResponseModelView
    {
        public Guid Id { get; set; }
        public Guid CourtId { get; set; }
        public string CourtName { get; set; } = string.Empty;
        public int StartTime { get; set; }
        public int EndTime { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
        public bool IsBooked { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
    }

    /// <summary>Sân + khung giờ trống trong một ngày – dùng hiển thị lịch.</summary>
    public class AvailableSlotModelView
    {
        public Guid CourtId { get; set; }
        public string CourtName { get; set; } = string.Empty;
        public int StartTime { get; set; }
        public int EndTime { get; set; }
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; }
        public string? BookedBy { get; set; }
    }

    /// <summary>DTO tạo khung giờ.</summary>
    public class CreateTimeFrameModelView
    {
        [System.ComponentModel.DataAnnotations.Required]
        public Guid CourtId { get; set; }

        /// <summary>Giờ bắt đầu (0-23).</summary>
        [System.ComponentModel.DataAnnotations.Required]
        public int StartTime { get; set; }

        /// <summary>Giờ kết thúc (0-23).</summary>
        [System.ComponentModel.DataAnnotations.Required]
        public int EndTime { get; set; }

        /// <summary>Thứ áp dụng (Mon, Tue…) hoặc "All".</summary>
        public string DayOfWeek { get; set; } = "All";
    }
}
