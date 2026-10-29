using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IReviewCommunityService
{
    Task ReportAsync(int reviewId, ReviewReportRequest request);
    Task<IReadOnlyList<ReviewReportView>> ReportsAsync(int? branchId, int page);
    Task ModerateAsync(int reviewId, ReviewModerationRequest request);
    Task ReplyAsync(int reviewId, ReviewReplyRequest request);
    Task<ReviewReplyView?> GetReplyAsync(int reviewId);
}
