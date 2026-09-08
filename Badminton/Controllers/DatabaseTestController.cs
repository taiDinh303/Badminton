//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using Repositories.Context;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class DatabaseTestController : ControllerBase
    {
        private readonly DatabaseContext _context;

        public DatabaseTestController(DatabaseContext context)
        {
            _context = context;
        }

//        [HttpGet("database")]
//        public async Task<IActionResult> TestDatabase()
//        {
//            var roles = await _context.Roles
//                .Select(x => new
//                {
//                    x.RoleId,
//                    x.Name
//                })
//                .ToListAsync();

//            return Ok(roles);
//        }
//    }
//}
