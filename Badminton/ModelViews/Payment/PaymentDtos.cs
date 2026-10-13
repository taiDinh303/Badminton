using M.Core.Enums;

namespace ModelViews.Payment
{
    // ---------- Payment ----------
    public record ConfirmPaymentRequest(int BookingId, PaymentMethod Method,
        string? TransactionReference);

    public record PaymentView(int Id, int BookingId, string BookingCode,
        PaymentMethod Method, decimal Amount, PaymentStatus Status,
        string? TransactionCode, DateTime? PaidAt);
}
