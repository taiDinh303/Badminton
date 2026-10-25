using Contract.Services.Review;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Review;

namespace Badminton.API.Controllers
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

        [HttpGet("booking/{bookingId:int}")]
        [Authorize]
        public async Task<IActionResult> ForBooking(int bookingId) => Resp(await _svc.GetByBookingAsync(bookingId));

        [HttpGet("paged")]
        [AllowAnonymous]
        public async Task<IActionResult> Paged([FromQuery] int? courtId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var paging = new Contract.Repositories.UnitOfWork.Page(page, Math.Min(pageSize, 100));
            var (items, total, average) = await _svc.GetPagedAsync(courtId, paging);
            return Resp(new { Items = items, TotalItems = total, CurrentPage = paging.Number, PageSize = paging.Size,
                TotalPages = (int)Math.Ceiling((double)total / paging.Size), AverageRating = average });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(CreateReviewRequest request) =>
            Resp(await _svc.CreateAsync(request, CurrentUserId), "Đánh giá thành công.");
    }
}
