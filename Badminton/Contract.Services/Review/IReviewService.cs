using ModelViews.Review;

namespace Contract.Services.Review
{
    public interface IReviewService
    {
        Task<ReviewView> CreateAsync(CreateReviewRequest request, int userId);
        Task<IReadOnlyList<ReviewView>> GetByCourtAsync(int courtId);
    }
}
