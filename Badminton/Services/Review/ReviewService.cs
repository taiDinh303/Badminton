using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Review;
using M.Core.Base;
using Microsoft.AspNetCore.Http;
using ModelViews.Review;
using System.Text.Json;

namespace Services
{
    public sealed class ReviewService : BaseService, IReviewService
    {
        private readonly IAuditService _audit;
        public ReviewService(IUnitOfWork uow, IHttpContextAccessor http, IAuditService audit)
            : base(uow, http) { _audit = audit; }

        private IGenericRepository<Review> Repo => Repository<Review>();

        public async Task<ReviewView> CreateAsync(CreateReviewRequest request, int userId)
        {
            if (request.Rating is < 1 or > 5)
                throw new BadRequestException("Điểm đánh giá phải từ 1 đến 5 sao.");

            var booking = await Repository<Booking>().GetByIdAsync(request.BookingId);
            if (booking is null) throw new NotFoundException("Booking", request.BookingId);
            if (booking.Status != M.Core.Enums.BookingStatus.Completed)
                throw new ConflictException("Chỉ đánh giá được đơn đã hoàn thành.");
            if (booking.UserId != userId)
                throw new ConflictException("Chỉ người đặt mới được đánh giá đơn này.");

            var dupes = await Repo.GetByConditionAsync(r => r.BookingId == request.BookingId);
            if (dupes.Count > 0)
                throw new ConflictException("Đơn này đã được đánh giá.");

            var court = await Repository<Court>().GetByIdAsync(booking.CourtId);
            var review = new Review
            {
                BookingId = booking.Id,
                CourtId = court?.Id ?? 0,
                UserId = userId,
                Rating = request.Rating,
                Comment = request.Comment,
                Images = request.Images is { Count: > 0 }
                    ? JsonSerializer.Serialize(request.Images)
                    : null
            };
            _ = Repo.AddAsync(review);

            // Rolling average: new_avg = (old_avg * count + new_rating) / (count+1).
            if (court is not null)
            {
                var newCount = court.RatingCount + 1;
                var newAvg = (court.RatingAvg ?? 0.0) * court.RatingCount + request.Rating;
                court.RatingAvg = Math.Round(newAvg / newCount, 2);
                court.RatingCount = newCount;
                court.UpdatedAt = DateTime.UtcNow;
                Repository<Court>().Update(court);
            }

            await SaveAsync();
            await _audit.LogAsync(userId, "review.create", "Review", review.Id.ToString(),
                null, review);
            return await ToView(review);
        }

        public async Task<IReadOnlyList<ReviewView>> GetByCourtAsync(int courtId)
        {
            var list = await Repo.GetByConditionAsync(r => r.CourtId == courtId);
            return (await Task.WhenAll(list.Select(ToView))).ToList();
        }

        private async Task<ReviewView> ToView(Review r)
        {
            var court = await Repository<Court>().GetByIdAsync(r.CourtId);
            var user = await Repository<User>().GetByIdAsync(r.UserId);
            var images = string.IsNullOrWhiteSpace(r.Images)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(r.Images) ?? new();
            return new ReviewView(r.Id, r.BookingId, r.CourtId, court?.Name ?? "?",
                user?.FullName ?? "?", r.Rating, r.Comment, images, r.CreatedAt,
                r.FlaggedRefunded);
        }
    }
}
