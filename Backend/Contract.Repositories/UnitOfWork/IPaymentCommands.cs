namespace Contract.Repositories.UnitOfWork;

public interface IPaymentCommands
{
    Task<int> ClearRefundReviewAsync(int id);
    Task<int> CompleteOnlineRefundAsync(int id);
    Task<int> RecordCheckoutAsync(int id, string url, string? transactionDate = null);
    Task<int> ClearInitializationReviewAsync(int id);
    Task<int> RequireReviewAsync(int id, string reason);
    Task<int> FailAsync(string gatewayRef);
    Task<int> SucceedAsync(string gatewayRef, string body, DateTime now);
}
