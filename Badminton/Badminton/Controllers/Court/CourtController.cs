using Contract.Services.Court;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Court;

namespace BadmintonBE.API.Controllers
{
    /// <summary>COURT endpoints: read the court list, admin manages courts.</summary>
    [ApiController]
    [Route("api/courts")]
    public class CourtController : BaseApiController
    {
        private readonly ICourtService _svc;

        public CourtController(ICourtService svc) => _svc = svc;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll() => Resp(await _svc.GetAllAsync());

        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Create(CreateCourtRequest request)
        {
            await _svc.CreateAsync(request, CurrentUserName);
            return RespMsg("Court created.");
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Update(int id, UpdateCourtRequest request)
        {
            await _svc.UpdateAsync(id, request, CurrentUserName);
            return RespMsg("Court updated.");
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteAsync(id, CurrentUserName);
            return RespMsg("Court deleted.");
        }
    }
}
