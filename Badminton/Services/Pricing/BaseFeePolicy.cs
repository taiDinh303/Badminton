using M.Core.Domain;

namespace Services.Pricing
{
    /// <summary>
    /// ABSTRACTION (trừu tượng) + INHERITANCE (kế thừa): a base fee policy that
    /// fixes the shared formula (base = price * hours, optional surcharges)
    /// and lets concrete policies only override the surcharge + policy name.
    /// This is the TEMPLATE-METHOD pattern: one flow, replaceable steps.
    /// </summary>
    public abstract class BaseFeePolicy : IFeePolicy
    {
        public abstract string PolicyName { get; }

        /// <summary>Optional per-context surcharge. Default = 0 (standard pricing).</summary>
        protected virtual decimal ComputeSurcharge(FeeContext context) => 0m;

        public decimal CalculateTotal(FeeContext context)
        {
            var baseAmount = context.PricePerHour * context.Hours;
            return baseAmount + ComputeSurcharge(context);
        }
    }
}
