using Contract.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Operations;
namespace Badminton.API.Controllers;

[ApiController, Authorize, Route("api/v1/notifications")]
public sealed class NotificationsController(IInboxService inbox, IDeliveryOperationsService deliveries) : BaseApiController
{
    [HttpGet("unread-count")]
    public async Task<IActionResult> Unread() => Resp(await inbox.UnreadCountAsync());
    [HttpGet]
    public async Task<IActionResult> List(int page = 1) => Resp(await inbox.ListAsync(page));
    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> Read(int id)
    {
        await inbox.ReadAsync(id);
        return RespMsg("Đã đọc.");
    }
    [HttpGet("preferences")]
    public async Task<IActionResult> Preferences() => Resp(await inbox.PreferencesAsync());
    [HttpPut("preferences")]
    public async Task<IActionResult> Preferences(PreferenceView request)
    {
        await inbox.SetPreferencesAsync(request);
        return RespMsg("Đã lưu tùy chọn.");
    }
    [HttpGet("deliveries"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Deliveries(int? branchId, int page = 1) => Resp(await deliveries.ListAsync(branchId, page));
    [HttpPost("deliveries/{id:int}/retry"), Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> Retry(int id)
    {
        await deliveries.RetryAsync(id);
        return RespMsg("Đã thử gửi lại; xem trạng thái thực tế trong danh sách.");
    }
}
