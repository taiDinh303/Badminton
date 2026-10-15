using Contract.Services.Voucher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Voucher;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/v1/vouchers")]
    public class VoucherController : BaseApiController
    {
        private readonly IVoucherService _svc;
        public VoucherController(IVoucherService svc) => _svc = svc;

        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true) =>
            Resp(await _svc.GetAllAsync(activeOnly));

        [HttpGet("{code}")]
        [Authorize]
        public async Task<IActionResult> GetByCode(string code) =>
            Resp(await _svc.GetByCodeAsync(code));

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CreateVoucherRequest request) =>
            Resp(await _svc.CreateAsync(request, CurrentUserId), "Tạo voucher thành công.");

        [HttpPost("{id:int}/deactivate")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Deactivate(int id)
        {
            await _svc.DeactivateAsync(id, CurrentUserId);
            return RespMsg("Vô hiệu voucher thành công.");
        }

        [HttpGet("{code}/preview")]
        [Authorize]
        public async Task<IActionResult> Preview(string code, [FromQuery] int baseAmount)
        {
            var discount = await _svc.PreviewDiscountAsync(code, CurrentUserId, baseAmount);
            return Resp(discount);
        }
    }
}
