using Contract.Services.Interface.Booking;
using Core.Base;
using Core.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.BookingModelView;

namespace API.Controllers
{
    [Route("api/bookings")]
    [ApiController]
    //[Authorize]
    public class BookingController : ControllerBase
    {
        private readonly IBookingService _bookingService;

        public BookingController(IBookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpGet("get-all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _bookingService.GetAllAsync();
            return Ok(new BaseResponse<List<BookingResponseModelView>>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: result));
        }

        [HttpGet("get-by-id/{id}")]
        //[Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _bookingService.GetByIdAsync(id);
            return Ok(new BaseResponse<BookingResponseModelView>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: result));
        }

        [HttpGet("my-bookings")]
        //[Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> GetMyBookings()
        {
            var userId = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var result = await _bookingService.GetByUserIdAsync(userId);
            return Ok(new BaseResponse<List<BookingResponseModelView>>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: result));
        }

        [HttpGet("get-by-user/{userInfoId}")]
        //[Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetByUser(string userInfoId)
        {
            var result = await _bookingService.GetByUserIdAsync(userInfoId);
            return Ok(new BaseResponse<List<BookingResponseModelView>>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: result));
        }

        [HttpPost("create")]
        //[Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> Create([FromBody] CreateBookingModelView model)
        {
            await _bookingService.CreateAsync(model);
            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Booking created successfully!"));
        }

        [HttpPut("update")]
        //[Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> Update([FromBody] UpdateBookingModelView model)
        {
            await _bookingService.UpdateAsync(model);
            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Booking updated successfully!"));
        }

        [HttpDelete("delete/{id}")]
        //[Authorize(Roles = "User,Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _bookingService.DeleteAsync(id);
            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Booking deleted successfully!"));
        }
    }
}
