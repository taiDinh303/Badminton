using Core.Abstractions;

namespace Core.Strategies.Pricing
{
    /// <summary>
    /// Chiến lược giá cho ngày thường (Thứ 2 – Thứ 6, không phải lễ).
    /// Hệ số = 1.0 (giá gốc).
    /// </summary>
    public class WeekdayPricingStrategy : IPricingStrategy
    {
        public string StrategyName => "Weekday";

        public decimal GetMultiplier(DateTime _) => 1.0m;

        public bool IsApplicable(DateTime date) =>
            date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }
}
