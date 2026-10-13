using ModelViews.CourtType;

namespace Contract.Services.CourtType
{
    public interface ICourtTypeService
    {
        Task<IReadOnlyList<CourtTypeView>> GetAllAsync();
        Task<int> CreateAsync(CreateCourtTypeRequest request, string userName);
        Task UpdateAsync(int id, UpdateCourtTypeRequest request, string userName);
        Task DeleteAsync(int id, string userName);
    }
}
