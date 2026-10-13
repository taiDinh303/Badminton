using M.Core.Domain;
using M.Core.Enums;

namespace Services
{
    /// <summary>
    /// POLYMORPHISM selection point (đa hình): the only place that maps a
    /// <see cref="PaymentMethod"/> to a concrete strategy. Built over an
    /// <c>IEnumerable&lt;IPaymentStrategy&gt;</c> injected from DI, so adding a
    /// new payment method = register one more strategy in DI; this factory
    /// and every caller stay untouched (Open/Closed).
    /// </summary>
    public sealed class PaymentStrategyFactory : IPaymentStrategyFactory
    {
        private readonly IReadOnlyDictionary<PaymentMethod, IPaymentStrategy> _map;

        public PaymentStrategyFactory(IEnumerable<IPaymentStrategy> strategies)
        {
            _map = strategies.ToDictionary(s => s.Method);
        }

        public IPaymentStrategy GetStrategy(PaymentMethod method) =>
            _map.TryGetValue(method, out var s)
                ? s
                : throw new M.Core.Base.BadRequestException(
                    $"Unsupported payment method '{method}'.");
    }
}
