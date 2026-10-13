using M.Core.Domain;

namespace Services.Pricing
{
    /// <summary>
    /// POLYMORPHISM factory (đa hình): pick a fee policy by court type name.
    /// Default = <see cref="StandardFeePolicy"/> so any court type without a
    /// dedicated policy still prices correctly (a safe fallback, not a crash).
    /// </summary>
    public sealed class FeePolicyFactory : IFeePolicyFactory
    {
        private static readonly IFeePolicy Standard = new StandardFeePolicy();

        public IFeePolicy GetPolicy(string courtTypeName)
        {
            var name = (courtTypeName ?? string.Empty).ToLowerInvariant();
            return name switch
            {
                "vip" => new PrimeTimeFeePolicy(),
                "premium" => new PrimeTimeFeePolicy(),
                "weekend" or "standard" => new WeekendFeePolicy(),
                _ => Standard
            };
        }
    }
}
