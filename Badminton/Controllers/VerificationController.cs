using Contract.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.Auth;

namespace BadmintonBE.API.Controllers
{
    [ApiController]
    [Route("api/verification")]
    [Authorize]
    public class VerificationController : ControllerBase
    {
        private readonly IVerificationService _verificationService;

        public VerificationController(
            IVerificationService verificationService)
        {
            _verificationService = verificationService;
        }

        [HttpPost("email/send")]
        public async Task<IActionResult> SendEmailCode()
        {
            var userId = GetUserId();

            await _verificationService
                .SendEmailVerificationAsync(userId);

            return Ok(new
            {
                message = "Đã tạo mã xác thực email."
            });
        }

        [HttpPost("email/verify")]
        public async Task<IActionResult> VerifyEmail(
            VerifyCodeRequest request)
        {
            var userId = GetUserId();

            await _verificationService
                .VerifyEmailAsync(userId, request.Code);

            return Ok(new
            {
                message = "Xác thực email thành công."
            });
        }

        [HttpPost("phone/send")]
        public async Task<IActionResult> SendPhoneCode()
        {
            var userId = GetUserId();

            await _verificationService
                .SendPhoneVerificationAsync(userId);

            return Ok(new
            {
                message = "Đã tạo mã xác thực số điện thoại."
            });
        }

        [HttpPost("phone/verify")]
        public async Task<IActionResult> VerifyPhone(
            VerifyCodeRequest request)
        {
            var userId = GetUserId();

            await _verificationService
                .VerifyPhoneAsync(userId, request.Code);

            return Ok(new
            {
                message = "Xác thực số điện thoại thành công."
            });
        }

        private int GetUserId()
        {
            var userId = User.FindFirst("UserId")?.Value;

            if (string.IsNullOrEmpty(userId))
                throw new Exception("Không tìm thấy UserId trong token.");

            return int.Parse(userId);
        }
    }
}