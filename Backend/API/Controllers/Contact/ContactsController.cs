using Contract.Services.Contact;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModelViews.Contact;

namespace Badminton.API.Controllers;

[ApiController]
[Route("api/v1/contacts")]
public sealed class ContactsController(IContactService service) : BaseApiController
{
    [HttpPost, AllowAnonymous, EnableRateLimiting("contact")]
    public async Task<IActionResult> Create(ContactRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length < 2)
            return BadRequest(new { data = (object?)null, message = "Vui lòng nhập họ tên hợp lệ.", statusCode = 400, code = "VALIDATION_ERROR" });
        await service.CreateAsync(request, ct);
        return RespMsg("Đã nhận yêu cầu tư vấn.");
    }

    [HttpGet, Authorize(Roles = "Admin")]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken ct = default)
    {
        var result = await service.ListAsync(page, ct);
        return Resp(new { items = result.Items, totalCount = result.TotalCount });
    }

    [HttpPatch("{id:int}/handled"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Handle(int id, CancellationToken ct) =>
        await service.MarkHandledAsync(id, ct) ? RespMsg("Đã xử lý.") : RespNotFound("Không tìm thấy yêu cầu.");
}
