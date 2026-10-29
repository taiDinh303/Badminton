using Contract.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Operations;
namespace Badminton.API.Controllers;

[ApiController, Authorize, Route("api/v1/community")]
public sealed class CommunityController(IReviewCommunityService reviews, ICourtFavoriteService favorites, IGroupAttendanceService groups, IPrivacyRequestService privacy) : BaseApiController
{
    [HttpGet("groups/{id:int}/meetings")]
    public async Task<IActionResult> Meetings(int id) => Resp(await groups.MeetingsAsync(id));
    [HttpGet("favorites")]
    public async Task<IActionResult> Favorites() => Resp(await favorites.FavoritesAsync());
    [HttpPut("favorites/{courtId:int}")]
    public async Task<IActionResult> Favorite(int courtId, [FromBody] bool enabled)
    {
        await favorites.FavoriteAsync(courtId, enabled);
        return RespMsg("Đã lưu sân yêu thích.");
    }
    [HttpPost("reviews/{id:int}/reports")]
    public async Task<IActionResult> Report(int id, ReviewReportRequest request)
    {
        await reviews.ReportAsync(id, request);
        return RespMsg("Đã tiếp nhận báo cáo.");
    }
    [HttpGet("review-reports"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Reports(int? branchId, int page = 1) => Resp(await reviews.ReportsAsync(branchId, page));
    [HttpPut("reviews/{id:int}/moderation"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Moderate(int id, ReviewModerationRequest request)
    {
        await reviews.ModerateAsync(id, request);
        return RespMsg("Đã lưu quyết định kiểm duyệt.");
    }
    [HttpPut("reviews/{id:int}/reply"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Reply(int id, ReviewReplyRequest request)
    {
        await reviews.ReplyAsync(id, request);
        return RespMsg("Đã lưu phản hồi chính thức.");
    }
    [HttpGet("reviews/{id:int}/reply")]
    public async Task<IActionResult> Reply(int id) => Resp(await reviews.GetReplyAsync(id));
    [HttpPost("privacy")]
    public async Task<IActionResult> Privacy(PrivacyRequestDto request) => Resp(await privacy.CreateAsync(request));
    [HttpGet("privacy")]
    public async Task<IActionResult> Privacy(int page = 1) => Resp(await privacy.ListAsync(page, false));
    [HttpGet("privacy/administration"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> PrivacyAdministration(int page = 1) => Resp(await privacy.ListAsync(page, true));
    [HttpPut("privacy/{id:int}/resolve"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Resolve(int id, ResolutionRequest request)
    {
        await privacy.ResolveAsync(id, request);
        return RespMsg("Đã lưu phản hồi yêu cầu dữ liệu.");
    }
}
