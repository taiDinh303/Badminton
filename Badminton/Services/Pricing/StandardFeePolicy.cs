namespace Services.Pricing
{
    /// <summary>Default pricing: base price * hours, no surcharge.</summary>
    public sealed class StandardFeePolicy : BaseFeePolicy
    {
        public override string PolicyName => "Standard";
    }
}
