using ModelViews.Review;

namespace Contract.Services.Review
{
    public interface IReviewService
    {
        Task<ReviewView?> GetByBookingAsync(int bookingId);
        Task<ReviewView> CreateAsync(CreateReviewRequest request, int userId);
        Task<IReadOnlyList<ReviewView>> GetByCourtAsync(int courtId);
        Task<(IReadOnlyList<ReviewView> Items, int Total, double? Average)> GetPagedAsync(int? courtId, Contract.Repositories.UnitOfWork.Page page);
    }
}
