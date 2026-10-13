using Core.Enums;

namespace Core.Abstractions
{
    /// <summary>
    /// Trừu tượng chung cho một cái sân thể thao.
    /// Các lớp sân cụ thể (IndoorCourt, OutdoorCourt…) kế thừa và hiện thực hoá
    /// các thuộc tính đặc thù (vật liệu, điều hoà, hệ thống chiếu sáng…).
    /// </summary>
    public interface ICourt
    {
        /// <summary>Định danh duy nhất của sân.</summary>
        Guid Id { get; }

        /// <summary>Tên hiển thị của sân.</summary>
        string CourtName { get; }

        /// <summary>Danh mục sân (trong nhà / ngoài trời).</summary>
        CourtCategory Category { get; }

        /// <summary>Giờ mở cửa (giờ, 0-23).</summary>
        int OpenTime { get; }

        /// <summary>Giờ đóng cửa (giờ, 0-23).</summary>
        int CloseTime { get; }

        /// <summary>Đơn giá cơ bản / giờ (VNĐ).</summary>
        int UnitPrice { get; }

        /// <summary>
        /// Tính giá cuối cùng cho một khung giờ đặt,
        /// dựa trên chiến lược giá hiện tại (đa hình qua IPricingStrategy).
        /// </summary>
        decimal CalculatePrice(DateTime bookingDate, int startHour, int endHour);

        /// <summary>Mô tả ngắn để hiển thị trên UI.</summary>
        string ToDescription();
    }
}
