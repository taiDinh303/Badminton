using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Review;
using Badminton.Core.Base;
using Badminton.Core.Security;
using ModelViews.Review;
using System.Text.Json;
using Contract.Services.Operations;

namespace Services
{
    public sealed class ReviewService : BaseService, IReviewService
    {
        private readonly IAuditService _audit;
        private readonly ICourtRatingProjection _ratings;
        public ReviewService(IUnitOfWork uow, ICurrentUser currentUser, IAuditService audit, ICourtRatingProjection ratings)
            : base(uow, currentUser) { _audit = audit; _ratings = ratings; }

        private IGenericRepository<Review> Repo => Repository<Review>();

        public async Task<ReviewView> CreateAsync(CreateReviewRequest request, int userId)
        {
            ReviewView result = null!;
            await Uow.RunInTransactionAsync(async () => result = await CreateCoreAsync(request, userId));
            return result;
        }

        private async Task<ReviewView> CreateCoreAsync(CreateReviewRequest request, int userId)
        {
            if (request.Rating is < 1 or > 5)
                throw new BadRequestException("Điểm đánh giá phải từ 1 đến 5 sao.");
            if (request.Comment?.Length > 2000)
                throw new BadRequestException("Nhận xét tối đa 2000 ký tự.");

            var booking = await Repository<Booking>().GetByIdAsync(request.BookingId);
            if (booking is null)
                throw new NotFoundException("Booking", request.BookingId);
            if (booking.Status != Badminton.Core.Enums.BookingStatus.Completed)
                throw new ConflictException("Chỉ đánh giá được đơn đã hoàn thành.");
            if (booking.UserId != userId)
                throw new ConflictException("Chỉ người đặt mới được đánh giá đơn này.");

            await Uow.LockBookingRequestAsync($"court-reviews:{booking.CourtId}");

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
                Comment = request.Comment?.Trim(),
                Images = request.Images is { Count: > 0 }
                    ? JsonSerializer.Serialize(request.Images)
                    : null
            };
            await Repo.AddAsync(review);

            await SaveAsync();
            if (court is not null)
                await _ratings.RebuildAsync(court.Id);
            await _audit.LogAsync(userId, "review.create", "Review", review.Id.ToString(),
                null, review);
            return await ToView(review);
        }

        public async Task<IReadOnlyList<ReviewView>> GetByCourtAsync(int courtId)
        {
            var list = await Repo.GetByConditionAsync(r => r.CourtId == courtId && !r.IsHidden);
            if (list.Count == 0)
                return Array.Empty<ReviewView>();

            var court = await Repository<Court>().GetByIdAsync(courtId);
            var userIds = list.Select(r => r.UserId).Distinct().ToList();
            var users = (await Repository<User>().GetByConditionAsync(u => userIds.Contains(u.Id)))
                .ToDictionary(u => u.Id);

            var views = new List<ReviewView>(list.Count);
            foreach (var r in list)
            {
                users.TryGetValue(r.UserId, out var user);
                views.Add(ToView(r, court?.Name ?? "?", user?.FullName ?? "?"));
            }
            return views;
        }

        public async Task<ReviewView?> GetByBookingAsync(int bookingId)
        {
            var booking = await Repository<Booking>().GetByIdAsync(bookingId) ?? throw new NotFoundException("Booking", bookingId);
            if (booking.UserId != _currentUser.Id)
                await AuthorizeStaffBranchAsync(booking.BranchId, _currentUser.Id);
            var row = (await Repo.GetByConditionAsync(r => r.BookingId == bookingId)).SingleOrDefault();
            return row is null ? null : await ToView(row);
        }

        public async Task<(IReadOnlyList<ReviewView> Items, int Total, double? Average)> GetPagedAsync(int? courtId, Page page)
        {
            System.Linq.Expressions.Expression<Func<Review, bool>> predicate = r => !r.IsHidden && (courtId == null || r.CourtId == courtId);
            var (items, count) = await Repo.GetPagedAsync(predicate, r => r.CreatedAt, OrderDirection.Desc, page);
            var totalStars = await Repo.SumIntByConditionAsync(predicate, r => r.Rating);
            var courtIds = items.Select(r => r.CourtId).Distinct().ToList();
            var userIds = items.Select(r => r.UserId).Distinct().ToList();
            var courts = (await Repository<Court>().GetByConditionAsync(c => courtIds.Contains(c.Id))).ToDictionary(c => c.Id);
            var users = (await Repository<User>().GetByConditionAsync(u => userIds.Contains(u.Id))).ToDictionary(u => u.Id);
            return (items.Select(r => ToView(r, courts.GetValueOrDefault(r.CourtId)?.Name ?? "?", users.GetValueOrDefault(r.UserId)?.FullName ?? "?")).ToList(),
                count, count == 0 ? null : Math.Round((double)totalStars / count, 2));
        }

        private async Task<ReviewView> ToView(Review r)
        {
            var court = await Repository<Court>().GetByIdAsync(r.CourtId);
            var user = await Repository<User>().GetByIdAsync(r.UserId);
            return ToView(r, court?.Name ?? "?", user?.FullName ?? "?");
        }

        private static ReviewView ToView(Review r, string courtName, string userName)
        {
            var images = string.IsNullOrWhiteSpace(r.Images)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(r.Images) ?? new();
            return new ReviewView(r.Id, r.BookingId, r.CourtId, courtName,
                userName, r.Rating, r.Comment, images, r.CreatedAt,
                r.FlaggedRefunded);
        }
    }
}
