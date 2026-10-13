namespace Core.Enums
{
    /// <summary>
    /// Trạng thái của một bản đặt sân.
    /// </summary>
    public enum BookingStatus
    {
        /// <summary>Đã tạo, chờ xác nhận thanh toán.</summary>
        Pending = 1,

        /// <summary>Đã xác nhận, sẵn sàng sử dụng.</summary>
        Confirmed = 2,

        /// <summary>Đã thanh toán đầy đủ.</summary>
        Paid = 3,

        /// <summary>Đã hoàn tất (đã sử dụng sân).</summary>
        Completed = 4,

        /// <summary>Đã hủy.</summary>
        Cancelled = 5,

        /// <summary>Đã hoàn tiền.</summary>
        Refunded = 6
    }
}
