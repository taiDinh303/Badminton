using Contract.Services.CourtType;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.CourtType;

namespace BadmintonBE.API.Controllers
{
    /// <summary>
    /// COURT-TYPE master endpoints. Reads are public; writes are staff/admin.
    /// Extends <see cref="BaseApiController"/> (INHERITANCE) for the Resp
    /// wrappers + CurrentUserName helper.
    /// </summary>
    [ApiController]
    [Route("api/court-types")]
    public class CourtTypeController : BaseApiController
    {
        private readonly ICourtTypeService _svc;

        public CourtTypeController(ICourtTypeService svc) => _svc = svc;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll() =>
            Resp(await _svc.GetAllAsync());

        [HttpGet("{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var all = await _svc.GetAllAsync();
            var x = all.FirstOrDefault(t => t.Id == id);
            return x is null
                ? RespNotFound($"Court type {id} not found.")
                : Resp(x);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Create(CreateCourtTypeRequest request)
        {
            var newId = await _svc.CreateAsync(request, CurrentUserName);
            return Resp(newId, "Court type created.");
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> Update(int id, UpdateCourtTypeRequest request)
        {
            await _svc.UpdateAsync(id, request, CurrentUserName);
            return RespMsg("Court type updated.");
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            await _svc.DeleteAsync(id, CurrentUserName);
            return RespMsg("Court type deleted.");
        }
    }
}
