using Microsoft.AspNetCore.Mvc;
using Repositories.Context;
using Microsoft.EntityFrameworkCore;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class DatabaseTestController : ControllerBase
    {
        private readonly BadmintonBookingDbContext _context;

        public DatabaseTestController(BadmintonBookingDbContext context)
        {
            _context = context;
        }

        [HttpGet("database")]
        public async Task<IActionResult> TestDatabase()
        {
            var roles = await _context.Roles
                .Select(x => new
                {
                    x.RoleId,
                    x.Name
                })
                .ToListAsync();

            return Ok(roles);
        }
    }
}
