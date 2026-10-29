using Contract.Services.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Badminton.API.Controllers;

[ApiController, Authorize, Route("api/v1/media")]
public sealed class MediaController(IMediaService media) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> List(int? courtId, int? reviewId) => Resp(await media.ListAsync(courtId, reviewId));
    [HttpPost, RequestSizeLimit(2200000)]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] int? courtId, [FromForm] int? reviewId, CancellationToken ct)
    {
        if (file.Length is <= 0 or > 2097152)
            return BadRequest(new
            {
                message = "Ảnh tối đa 2 MB."
            });
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        return Resp(await media.UploadAsync(stream.ToArray(), courtId, reviewId, ct));
    }
    [HttpGet("{id:int}"), AllowAnonymous]
    public async Task<IActionResult> Read(int id, CancellationToken ct)
    {
        var result = await media.ReadAsync(id, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(result.Bytes, result.ContentType);
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await media.DeleteAsync(id, ct);
        return RespMsg("Đã xóa ảnh.");
    }
    [HttpPut("{id:int}/avatar")]
    public async Task<IActionResult> Avatar(int id)
    {
        await media.SetAvatarAsync(id);
        return RespMsg("Đã cập nhật ảnh đại diện.");
    }
}
