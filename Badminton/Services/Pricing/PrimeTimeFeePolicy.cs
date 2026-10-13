using M.Core.Domain;

namespace Services.Pricing
{
    /// <summary>
    /// Prime-time surcharge: +150k/hour on weekend evenings, +50k/hour on
    /// weekday evenings (prime time = 18:00-21:00).
    /// </summary>
    public sealed class PrimeTimeFeePolicy : BaseFeePolicy
    {
        public const decimal WeekendEveningSurchargePerHour = 150_000m;
        public const decimal WeekdayEveningSurchargePerHour = 50_000m;

        public override string PolicyName => "PrimeTime";

        protected override decimal ComputeSurcharge(FeeContext context)
        {
            if (!context.IsPrimeTime) return 0m;
            var perHour = context.IsWeekend
                ? WeekendEveningSurchargePerHour
                : WeekdayEveningSurchargePerHour;
            return perHour * context.Hours;
        }
    }
}
