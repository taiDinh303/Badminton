using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IDisruptionService
{
    Task<int> AnnounceAsync(IncidentRequest request);
    Task<IReadOnlyList<DisruptionView>> ListAsync(int? branchId, int page);
    Task RecordContactAsync(int id, ContactOutcomeRequest request);
}
