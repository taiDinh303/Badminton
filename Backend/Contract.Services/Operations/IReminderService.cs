using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IReminderService
{
    Task<int> RunAsync();
}
