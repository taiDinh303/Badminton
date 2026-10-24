using Badminton.Core.Domain;
using ModelViews.Payment;

namespace Contract.Services.Payment
{
    public interface IPaymentService
    {
        Task<PaymentView> ReconcileRefundAsync(int paymentId, int actorId);
        Task<(IReadOnlyList<PaymentReconciliationView> Items, int TotalCount)> GetReconciliationAsync(int? branchId, Contract.Repositories.UnitOfWork.Page page);
        Task<PaymentInitResponse> InitAsync(InitPaymentRequest request, int userId);
        Task<WebhookResult> HandleWebhookAsync(string provider, PaymentWebhookRequest raw);
        Task<PaymentView> ConfirmOnSiteAsync(int bookingId, string method, int actorId, string? note = null);
        Task<PaymentView> RefundForMaintenanceAsync(int bookingId, int actorId, string? reason = null);
        Task<PaymentView> RefundAsync(int bookingId, int actorId, string? reason = null);
        Task<PaymentView> CancelRefundAsync(int bookingId, int actorId, string? reason = null);
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
