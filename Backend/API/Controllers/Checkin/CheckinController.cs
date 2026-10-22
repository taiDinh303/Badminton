using Contract.Services.Booking;
using Badminton.Core.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Badminton.API.Controllers
{
    [ApiController]
    [Route("api/v1/checkin")]
    [Authorize(Roles = "Admin,Manager")]
    public class CheckinController : BaseApiController
    {
        private readonly ICheckinService _svc;
        public CheckinController(ICheckinService svc) => _svc = svc;

        public sealed record TokenRequest([System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(128)] string Token);

        [HttpPost("token")]
        public async Task<IActionResult> ByToken(TokenRequest request) => Resp(new
        {
            BookingId = (await _svc.CheckinByTokenAsync(request.Token.Trim(), CurrentUserId)).Id,
            Message = "Đã check-in thành công."
        });

        [HttpPost("qr/{token}")]
        public async Task<IActionResult> ByQr(string token)
            => Resp(new { BookingId = (await _svc.CheckinByTokenAsync(token, CurrentUserId)).Id,
                          Message = "Đã check-in thành công." },
                "Check-in thành công.");

        [HttpPost("code/{code}")]
        public async Task<IActionResult> ByCode(string code)
            => Resp(new { BookingId = (await _svc.CheckinByCodeAsync(code, CurrentUserId)).Id,
                          Message = "Đã check-in thành công." },
                "Check-in thành công.");

        [HttpGet("slot/{courtId:int}/{date}/{slotLabel}")]
        public async Task<IActionResult> SlotList(int courtId, DateOnly date, string slotLabel)
            => Resp((await _svc.GetUnconfirmedForSlotAsync(courtId, date, slotLabel))
                .Select(b => new { b.Id, b.BookingCode, b.Status, b.CheckedInAt }));
    }
}
