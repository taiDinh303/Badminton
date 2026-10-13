using Core.Enums;

namespace Core.Abstractions
{
    /// <summary>
    /// Strategy: chiến lược tính giá cho sân theo loại ngày.
    /// Có thể thay đổi tại runtime mà không cần sửa lại logic tính giá của sân.
    /// </summary>
    public interface IPricingStrategy
    {
        /// <summary>Tên chiến lược, dùng để truy vấn / cấu hình.</summary>
        string StrategyName { get; }

        /// <summary>
        /// Trả về hệ số nhân giá cơ bản cho ngày cho trước.
        /// Ví dụ: cuối tuần → 1.5, ngày thường → 1.0, lễ → 2.0
        /// </summary>
        decimal GetMultiplier(DateTime bookingDate);

        /// <summary>
        /// Kiểm tra xem ngày này có áp dụng chiến lược này không.
        /// Dùng để tự động chọn chiến lược phù hợp khi không chỉ định rõ.
        /// </summary>
        bool IsApplicable(DateTime bookingDate);
    }
}
