using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IDeliveryOperationsService
{
    Task<IReadOnlyList<DeliveryView>> ListAsync(int? branchId, int page);
    Task RetryAsync(int id);
}
