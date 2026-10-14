using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using M.Core.Base;
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
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 100)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 500);

            DateOnly? f = DateOnly.TryParse(from, out var fd) ? fd : null;
            DateOnly? t = DateOnly.TryParse(to, out var td) ? td : null;

            var (items, total) = await _svc.QueryPagedAsync(entityType, entityId, f, t,
                new Page(page, pageSize));

            return Resp(new BasePaginatedList<AuditEntry>(items, total, page, pageSize));
        }
    }
}
