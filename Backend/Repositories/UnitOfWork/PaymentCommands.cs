using Badminton.Core.Enums;
using Contract.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UnitOfWork;

internal sealed class PaymentCommands(BadmintonBookingDbContext db) : IPaymentCommands
{
    public Task<int> CompleteOnlineRefundAsync(int id) => db.Set<Contract.Repositories.Entity.OnlineRefundOperation>()
        .Where(o => o.Id == id && !o.Succeeded).ExecuteUpdateAsync(s => s.SetProperty(o => o.Succeeded, true));
    public Task<int> ClearRefundReviewAsync(int id) => db.Payments.Where(p => p.Id == id && p.ReconciliationReason == "refund_uncertain")
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.ReconciliationRequired, false).SetProperty(p => p.ReconciliationReason, (string?)null));
    public Task<int> RecordCheckoutAsync(int id, string url, string? transactionDate = null) => db.Payments.Where(p => p.Id == id)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.GatewayUrl, url)
            .SetProperty(p => p.ProviderTransactionDate, p => transactionDate ?? p.ProviderTransactionDate));

    public Task<int> ClearInitializationReviewAsync(int id) => db.Payments
        .Where(p => p.Id == id && p.ReconciliationReason == "initialization_uncertain")
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.ReconciliationRequired, false).SetProperty(p => p.ReconciliationReason, (string?)null));

    public Task<int> RequireReviewAsync(int id, string reason) => db.Payments.Where(p => p.Id == id)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.ReconciliationRequired, true).SetProperty(p => p.ReconciliationReason, reason));

    public Task<int> FailAsync(string gatewayRef) => db.Payments
        .Where(p => p.GatewayRef == gatewayRef && p.Status == PaymentStatus.Pending)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Failed));

    public Task<int> SucceedAsync(string gatewayRef, string body, DateTime now) => db.Payments
        .Where(p => p.GatewayRef == gatewayRef &&
            (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Failed))
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PaymentStatus.Success)
            .SetProperty(p => p.PaidAt, now).SetProperty(p => p.RawResponse, body));
}
