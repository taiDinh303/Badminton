using Contract.Repositories.AuthEntity;

namespace Contract.Repositories.IOUW
{
    public interface ICourtRepository
    {
        Task<List<Court>> GetAllAsync();
        Task<Court?> GetByIdAsync(int courtId);
    }
}