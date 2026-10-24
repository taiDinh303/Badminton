using Badminton.Core.Enums;

namespace Badminton.Core.Domain;

public sealed record OnlineRefundRequest(PaymentMethod Method, string RequestId, string PaymentReference,
    string TransactionId, string TransactionDate, int Amount, int OriginalAmount, string Reason, DateTime CreatedAt);
public sealed record OnlineRefundOutcome(bool Succeeded, string Code)
{
    public static OnlineRefundOutcome Pending(string code = "refund_uncertain") => new(false, code);
}
public interface IOnlineRefundClient
{
    string CreateRequestId(PaymentMethod method);
    Task<OnlineRefundOutcome> ExecuteAsync(OnlineRefundRequest request, bool queryOnly, CancellationToken ct = default);
}
