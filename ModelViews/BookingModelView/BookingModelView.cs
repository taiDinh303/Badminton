using Core.Enums;
using ModelViews.BookingModelView;

namespace ModelViews.BookingModelView
{
    /// <summary>
    /// DTO tạo bản đặt sân.
    /// </summary>
    public class CreateBookingModelView
    {
        [Required]
        public Guid CourtId { get; set; }

        [Required]
        public Guid TimeFrameId { get; set; }

        [Required]
        public DateTime BookingDate { get; set; }

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Note { get; set; }
    }

    /// <summary>
    /// DTO cập nhật trạng thái / thanh toán của bản đặt.
    /// </summary>
    public class UpdateBookingModelView
    {
        [Required]
        public Guid Id { get; set; }

        /// <summary>Trạng thái muốn chuyển (xác nhận / hủy / hoàn tiền…).</summary>
        public BookingStatus? Status { get; set; }

        /// <summary>Chọn phương thức thanh toán (nếu có).</summary>
        public PaymentMethod? PaymentMethod { get; set; }
    }

    /// <summary>
    /// DTO trả về bản đặt – thể hiện đầy đủ dữ liệu cho UI.
    /// </summary>
    public class BookingResponseModelView
    {
        public Guid Id { get; set; }
        public string BookingCode { get; set; } = string.Empty;
        public Guid CourtId { get; set; }
        public string? CourtName { get; set; }
        public CourtCategory? CourtCategory { get; set; }
        public Guid TimeFrameId { get; set; }
        public int? StartHour { get; set; }
        public int? EndHour { get; set; }
        public DateTime BookingDate { get; set; }
        public DateTime? CancellationDeadline { get; set; }
        public decimal Price { get; set; }
        public BookingStatus Status { get; set; }
        public string? AppliedPricingStrategy { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }
        public string? TransactionCode { get; set; }
        public string UserInfoId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTimeOffset CreatedTime { get; set; }
        public DateTimeOffset? LastUpdatedTime { get; set; }
    }
}
