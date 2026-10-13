using M.Core.Domain;
using ModelViews.Payment;

namespace Contract.Services.Payment
{
    public interface IPaymentService
    {
        Task<PaymentInitResponse> InitAsync(InitPaymentRequest request, int userId);
        Task HandleWebhookAsync(string provider, PaymentWebhookRequest raw);
        Task<PaymentView> ConfirmOnSiteAsync(int bookingId, string method, int actorId,
            string? note = null);
        Task<PaymentView> RefundAsync(int bookingId, int actorId, string? reason = null);

        Task<IReadOnlyList<PaymentView>> GetByBookingAsync(int bookingId);
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
