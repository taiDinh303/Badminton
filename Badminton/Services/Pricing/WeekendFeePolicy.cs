using M.Core.Domain;

namespace Services.Pricing
{
    /// <summary>Weekend surcharge: +100k VND per hour on Sat/Sun.</summary>
    public sealed class WeekendFeePolicy : BaseFeePolicy
    {
        public const decimal WeekendSurchargePerHour = 100_000m;

        public override string PolicyName => "Weekend";

        protected override decimal ComputeSurcharge(FeeContext context) =>
            context.IsWeekend ? WeekendSurchargePerHour * context.Hours : 0m;
    }
}
