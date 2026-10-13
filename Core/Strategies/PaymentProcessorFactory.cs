using Core.Abstractions;
using Core.Enums;
using Core.Strategies.Payment;

namespace Core.Strategies
{
    /// <summary>
    /// Factory: đóng gói việc tạo đúng IPaymentProcessor theo phương thức.
    /// Client chỉ cần gọi factory, không biết về các lớp con cụ thể.
    /// </summary>
    public sealed class PaymentProcessorFactory
    {
        private readonly Dictionary<PaymentMethod, IPaymentProcessor> _registry;

        public PaymentProcessorFactory()
        {
            _registry = new Dictionary<PaymentMethod, IPaymentProcessor>
            {
                [PaymentMethod.Cash]        = new CashPaymentProcessor(),
                [PaymentMethod.BankTransfer] = new BankTransferPaymentProcessor(),
                [PaymentMethod.Card]        = new CardPaymentProcessor(),
                [PaymentMethod.QrCode]      = new QrCodePaymentProcessor()
            };
        }

        /// <summary>
        /// Lấy đúng processor cho phương thức thanh toán.
        /// Ném ArgumentException nếu không tồn tại.
        /// </summary>
        public IPaymentProcessor Create(PaymentMethod method)
        {
            if (!_registry.TryGetValue(method, out var processor))
                throw new ArgumentException(
                    $"Không tìm thấy processor cho phương thức: {method}", nameof(method));

            return processor;
        }

        /// <summary>Lấy toàn bộ phương thức đang hỗ trợ.</summary>
        public IReadOnlyCollection<PaymentMethod> SupportedMethods() =>
            _registry.Keys.ToArray();
    }
}
