using Badminton.Core.Base;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class ReviewCommunityService(IUnitOfWork uow, IOperationsAccess access, IAuditService audit, ICourtRatingProjection ratings, IOperationsReadStore reads) : IReviewCommunityService
{
    public async Task ReportAsync(int reviewId, ReviewReportRequest request)
    {
        await access.ActorAsync();
        var reason = OperationInput.Text(request.Reason, 1500);
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockBookingRequestAsync($"review-community:{reviewId}");
            var review = await uow.GetRepository<Review>().GetByIdAsync(reviewId);
            if (review is null || review.IsHidden)
                throw new NotFoundException("Review", reviewId);
            var repo = uow.GetRepository<ReviewReport>();
            if (await repo.CountByConditionAsync(r => r.ReviewId == reviewId && r.UserId == access.ActorId) > 0)
                throw new ConflictException("Bạn đã báo cáo đánh giá này.");
            await repo.AddAsync(new()
            {
                ReviewId = reviewId,
                UserId = access.ActorId,
                Reason = reason
            });
            await uow.SaveChangesAsync();
        });
    }
    public async Task<IReadOnlyList<ReviewReportView>> ReportsAsync(int? branchId, int page)
    {
        var branch = await access.StaffBranchAsync(branchId);
        var rows = await reads.ReviewReportsAsync(branch, new Page(page, 30));
        var reviewIds = rows.Select(r => r.ReviewId).Distinct().ToArray();
        var reviews = (await uow.GetRepository<Review>().GetByConditionAsync(r => reviewIds.Contains(r.Id))).ToDictionary(r => r.Id);
        return rows.Select(r => new ReviewReportView(r.Id, r.ReviewId, r.Reason, r.Status.ToString(), r.Resolution, reviews[r.ReviewId].Comment, reviews[r.ReviewId].Rating)).ToArray();
    }
    public async Task ModerateAsync(int reviewId, ReviewModerationRequest request)
    {
        var reason = OperationInput.Text(request.Reason, 1500);
        await uow.RunInTransactionAsync(async () =>
        {
            var repo = uow.GetRepository<Review>();
            var preview = (await repo.GetByConditionAsync(r => r.Id == reviewId)).SingleOrDefault() ?? throw new NotFoundException("Review", reviewId);
            var court = await uow.GetRepository<Court>().GetByIdAsync(preview.CourtId) ?? throw new NotFoundException("Court", preview.CourtId);
            await access.StaffBranchAsync(court.BranchId);
            await uow.LockBookingRequestAsync($"court-reviews:{court.Id}");
            await uow.LockBookingRequestAsync($"review-community:{reviewId}");
            var review = (await repo.GetByIdAsync(reviewId))!;
            review.IsHidden = request.Hide;
            review.ModerationReason = reason;
            repo.UpdateProperties(review, nameof(Review.IsHidden), nameof(Review.ModerationReason));
            var reports = await uow.GetRepository<ReviewReport>().GetByConditionAsync(r => r.ReviewId == reviewId && r.Status == CaseStatus.Open);
            foreach (var report in reports)
            {
                report.Status = request.Hide ? CaseStatus.Accepted : CaseStatus.Rejected;
                report.Resolution = reason;
                report.ResolvedBy = access.ActorId;
                uow.GetRepository<ReviewReport>().Update(report);
            }
            await uow.SaveChangesAsync();
            await ratings.RebuildAsync(court.Id);
            await audit.LogAsync(access.ActorId, "review.moderated", "Review", reviewId.ToString(), null, new
            {
                review.IsHidden
            }, reason);
        });
    }
    public async Task ReplyAsync(int reviewId, ReviewReplyRequest request)
    {
        var text = OperationInput.Text(request.Text, 2000, 2);
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockBookingRequestAsync($"review-community:{reviewId}");
            var review = await uow.GetRepository<Review>().GetByIdAsync(reviewId) ?? throw new NotFoundException("Review", reviewId);
            var court = await uow.GetRepository<Court>().GetByIdAsync(review.CourtId) ?? throw new NotFoundException("Court", review.CourtId);
            await access.StaffBranchAsync(court.BranchId);
            var repo = uow.GetRepository<ReviewReply>();
            var row = (await repo.GetByConditionAsync(r => r.ReviewId == reviewId)).SingleOrDefault();
            var before = row?.Text;
            if (row is null)
            {
                row = new()
                {
                    ReviewId = reviewId
                };
                await repo.AddAsync(row);
            }
            else
                repo.Update(row);
            row.Text = text;
            row.ActorId = access.ActorId;
            row.UpdatedAt = DateTime.UtcNow;
            await uow.SaveChangesAsync();
            await audit.LogAsync(access.ActorId, "review.replied", "Review", reviewId.ToString(), new
            {
                Text = before
            }, new
            {
                Text = text
            });
        });
    }
    public async Task<ReviewReplyView?> GetReplyAsync(int reviewId)
    {
        var review = await uow.GetRepository<Review>().GetByIdAsync(reviewId);
        if (review is null || review.IsHidden)
            return null;
        var row = (await uow.GetRepository<ReviewReply>().GetByConditionAsync(r => r.ReviewId == reviewId)).SingleOrDefault();
        return row is null ? null : new(row.Id, row.ReviewId, row.Text, row.CreatedAt, row.UpdatedAt);
    }
}
