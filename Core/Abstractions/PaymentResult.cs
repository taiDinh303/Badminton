namespace Core.Abstractions
{
    /// <summary>
    /// Kết quả của một lần xử lý thanh toán.
    /// </summary>
    public record PaymentResult(
        bool Success,
        string? TransactionCode,
        string? Message);

    /// <summary>
    /// Trạng thái giao dịch thanh toán.
    /// </summary>
    public record PaymentStatusInfo(
        string TransactionCode,
        PaymentState State,
        decimal Amount,
        DateTimeOffset? CompletedAt,
        string? Remark)
    {
        public static PaymentStatusInfo Unknown(string code, string remark = "") =>
            new(code, PaymentState.Unknown, 0, null, remark);

        public static PaymentStatusInfo Pending(string code, decimal amount) =>
            new(code, PaymentState.Pending, amount, null, null);

        public static PaymentStatusInfo Success(string code, decimal amount, DateTimeOffset at, string? remark = null) =>
            new(code, PaymentState.Success, amount, at, remark);

        public static PaymentStatusInfo Failed(string code, string remark) =>
            new(code, PaymentState.Failed, 0, null, remark);

        public static PaymentStatusInfo Refunded(string code, decimal amount, DateTimeOffset at) =>
            new(code, PaymentState.Refunded, amount, at, null);
    }

    public enum PaymentState
    {
        Unknown,
        Pending,
        Success,
        Failed,
        Refunded
    }
}
