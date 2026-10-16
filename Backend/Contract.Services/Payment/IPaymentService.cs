using Badminton.Core.Domain;
using ModelViews.Payment;

namespace Contract.Services.Payment
{
    public interface IPaymentService
    {
        Task<PaymentInitResponse> InitAsync(InitPaymentRequest request, int userId);

        /// <summary>
        /// Verify the provider's signature on <paramref name="raw"/> via the
        /// gateway, then apply the result idempotently and atomically.
        /// Safe to call any number of times with the same <c>GatewayRef</c>:
        /// only the first call mutates; later calls return
        /// <see cref="WebhookResult.Duplicate"/>.
        /// </summary>
        Task<WebhookResult> HandleWebhookAsync(string provider, PaymentWebhookRequest raw);

        Task<PaymentView> ConfirmOnSiteAsync(int bookingId, string method, int actorId,
            string? note = null);

        Task<PaymentView> RefundAsync(int bookingId, int actorId, string? reason = null);

        /// <summary>
        /// Read the payment rows for <paramref name="bookingId"/>. Only the
        /// booking owner or a staff member (Admin/Manager) may read them;
        /// any other user is rejected (IDOR protection).
        /// </summary>
        Task<IReadOnlyList<PaymentView>> GetByBookingAsync(int bookingId, int requestingUserId);
    }

    public static class RefundPolicy
    {
        public static (double ratio, string label) Tier(DateTime now, DateTime slotStart)
        {
            var ahead = (slotStart - now).TotalHours;
            return ahead switch
            {
                >= 2 => (1.0, "100% refund (≥2h before start)"),
                >= 1 => (0.5, "50% refund (1–2h before start)"),
                _ => (0.0, "no refund (<1h before start / no-show)")
            };
        }
    }
}
