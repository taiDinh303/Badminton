using ModelViews.Court;

namespace Contract.Services.Interface
{
    public interface ICourtService
    {
        Task<List<CourtResponse>> GetAllAsync();
        Task<CourtResponse?> GetByIdAsync(int courtId);
    }
}