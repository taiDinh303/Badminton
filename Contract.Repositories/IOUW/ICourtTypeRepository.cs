using Contract.Repositories.Entity;

namespace Contract.Repositories.IOUW
{
    public interface ICourtTypeRepository
    {
        Task<List<CourtType>> GetAllAsync();
        Task<CourtType?> GetByIdAsync(int id);
        Task<CourtType> CreateAsync(CourtType courtType);
        Task<CourtType?> UpdateAsync(CourtType courtType);
        Task<bool> DeleteAsync(int id);
    }
}