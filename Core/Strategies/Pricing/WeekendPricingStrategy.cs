using Core.Abstractions;

namespace Core.Strategies.Pricing
{
    /// <summary>
    /// Chiến lược giá cuối tuần (Thứ 7, Chủ nhật).
    /// Hệ số = 1.5 (sân đông khách hơn, giá cao hơn).
    /// </summary>
    public class WeekendPricingStrategy : IPricingStrategy
    {
        private static readonly decimal WeekendMultiplier = 1.5m;

        public string StrategyName => "Weekend";

        public decimal GetMultiplier(DateTime _) => WeekendMultiplier;

        public bool IsApplicable(DateTime date) =>
            date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }
}
