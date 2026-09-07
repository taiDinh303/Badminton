using Contract.Repositories.Entity;

namespace Contract.Repositories.IOUW
{
    public interface ICourtRepository
    {
        Task<List<Court>> GetAllAsync();
        Task<Court?> GetByIdAsync(int courtId);
    }
}