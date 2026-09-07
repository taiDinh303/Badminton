using ModelViews.CourtType;

namespace Contract.Services.Interface
{
    public interface ICourtTypeService
    {
        Task<List<CourtTypeResponse>> GetAllAsync();
        Task<CourtTypeResponse?> GetByIdAsync(int id);
        Task<CourtTypeResponse> CreateAsync(CourtTypeRequest request);
        Task<CourtTypeResponse?> UpdateAsync(int id, CourtTypeRequest request);
        Task<bool> DeleteAsync(int id);
    }
}