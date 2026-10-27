using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IPrivacyRequestService
{
    Task<int> CreateAsync(PrivacyRequestDto request);
    Task<IReadOnlyList<PrivacyView>> ListAsync(int page, bool administration);
    Task ResolveAsync(int id, ResolutionRequest request);
}
