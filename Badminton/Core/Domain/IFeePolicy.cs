namespace M.Core.Domain
{
    /// <summary>
    /// ABSTRACTION (trừu tượng) + POLYMORPHISM (đa hình) for court pricing.
    /// Different fee rules (standard court vs VIP/prime-time) implement this
    /// without the booking service knowing the specifics.
    /// </summary>
    public interface IFeePolicy
    {
        /// <summary>Human-readable policy name, stored on the booking for audit.</summary>
        string PolicyName { get; }

        /// <summary>Compute the total fee for a set of court-hours on a given date.</summary>
        decimal CalculateTotal(FeeContext context);
    }

    /// <summary>Inputs a fee policy needs to compute the charge.</summary>
    public sealed record FeeContext
    {
        /// <summary>Base hourly price of the court.</summary>
        public required decimal PricePerHour { get; init; }

        /// <summary>Number of hours booked.</summary>
        public required int Hours { get; init; }

        /// <summary>Is the booking on a weekend date (for surcharges).</summary>
        public bool IsWeekend { get; init; }

        /// <summary>Is the booking during prime time (evening).</summary>
        public bool IsPrimeTime { get; init; }
    }

    /// <summary>Factory that picks the right fee policy for a court type.</summary>
    public interface IFeePolicyFactory
    {
        IFeePolicy GetPolicy(string courtTypeName);
    }
}
