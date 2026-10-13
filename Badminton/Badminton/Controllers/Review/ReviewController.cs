using Contract.Services.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Review;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/v1/reviews")]
    public class ReviewController : BaseApiController
    {
        private readonly IReviewService _svc;
        public ReviewController(IReviewService svc) => _svc = svc;

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetByCourt([FromQuery] int courtId) =>
            Resp(await _svc.GetByCourtAsync(courtId));

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(CreateReviewRequest request) =>
            Resp(await _svc.CreateAsync(request, CurrentUserId), "Đánh giá thành công.");
    }
}
