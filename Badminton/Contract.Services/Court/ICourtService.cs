using ModelViews.Court;

namespace Contract.Services.Court
{
    public interface ICourtService
    {
        Task<IReadOnlyList<CourtView>> GetAllAsync();
        Task CreateAsync(CreateCourtRequest request, string userName);
        Task UpdateAsync(int id, UpdateCourtRequest request, string userName);
        Task DeleteAsync(int id, string userName);
    }
}
