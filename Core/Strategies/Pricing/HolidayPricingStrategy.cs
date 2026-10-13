using Core.Abstractions;

namespace Core.Strategies.Pricing
{
    /// <summary>
    /// Chiến lược giá cho ngày lễ / ngày cao điểm.
    /// Hệ số = 2.0 (giá gấp đôi so với ngày thường).
    /// Danh sách ngày lễ được cấu hình từ bên ngoài (chấm thời gian 2024+).
    /// </summary>
    public class HolidayPricingStrategy : IPricingStrategy
    {
        private static readonly decimal HolidayMultiplier = 2.0m;

        private readonly HashSet<DateTime> _holidays;

        /// <summary>
        /// Nhận danh sách ngày lễ để xác định có áp dụng giá lễ không.
        /// </summary>
        public HolidayPricingStrategy(IEnumerable<DateTime>? holidays = null)
        {
            _holidays = holidays?.ToHashSet() ?? new HashSet<DateTime>
            {
                // Mặc định: lễ lớn trong năm
                new DateTime(DateTime.Now.Year, 1, 1),
                new DateTime(DateTime.Now.Year, 2, 4).AddDays(-7), // Giỗ tổ 3/2
                new DateTime(DateTime.Now.Year, 4, 30),
                new DateTime(DateTime.Now.Year, 9, 2),
                new DateTime(DateTime.Now.Year, 12, 22).AddDays(1)  // Năm mới
            };
        }

        public string StrategyName => "Holiday";

        public decimal GetMultiplier(DateTime _) => HolidayMultiplier;

        public bool IsApplicable(DateTime date) =>
            _holidays.Contains(date.Date);
    }
}
