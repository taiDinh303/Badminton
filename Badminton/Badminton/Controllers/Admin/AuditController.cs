using Contract.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/v1/admin/audit")]
    [Authorize(Roles = "Admin")]
    public class AuditController : BaseApiController
    {
        private readonly IAuditService _svc;
        public AuditController(IAuditService svc) => _svc = svc;

        [HttpGet]
        public async Task<IActionResult> Query(
            [FromQuery] string? entityType = null,
            [FromQuery] string? entityId = null,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            [FromQuery] int limit = 100)
        {
            DateOnly? f = DateOnly.TryParse(from, out var fd) ? fd : null;
            DateOnly? t = DateOnly.TryParse(to, out var td) ? td : null;
            var entries = await _svc.QueryAsync(entityType, entityId, f, t, limit);
            return Resp(entries);
        }
    }
}
