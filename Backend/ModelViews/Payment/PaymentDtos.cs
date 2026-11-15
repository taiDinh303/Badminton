namespace ModelViews.Payment
{
    public record InitPaymentRequest(int BookingId, string Method, string? ReturnUrl = null);

    public record PaymentWebhookRequest(string GatewayRef, string Status,
        string? RawResponse = null, int? Amount = null);

    public record PaymentView(int Id, int BookingId, string Method, int Amount,
        string Status, string? GatewayRef, DateTime? PaidAt, int RefundAmount,
        DateTime? RefundedAt);

    public record PaymentReconciliationView(PaymentView Payment, string? Reason, DateTime CreatedAt);

    public record PaymentInitResponse(string Method, string? GatewayUrl,
        string? GatewayRef, string BookingCode)
    {
        public BankTransferView? BankTransfer { get; init; }
    }

    public record BankTransferView(string BankBin, string AccountNumber, string AccountName,
        int Amount, string Currency, string TransferContent, string QrImageUrl,
        DateTime ExpiresAtUtc, bool RequiresStaffConfirmation);
}
