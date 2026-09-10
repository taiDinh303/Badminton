using Contract.Repositories.Entity;
using Contract.Services.Interface;
using Core.Base;
using Core.Store;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ModelViews.AuthModelView;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // No Authorize
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthController(IAuthService authService, UserManager<ApplicationUser> userManager)
        {
            _authService = authService;
            _userManager = userManager;
        }

        /// <summary>
        /// Login
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModelView model)
        {
            var result = await _authService.LoginAsync(model);

            return Ok(BaseResponse<AuthResponseModelView>.OkResponse(
                result,
                ResponseCodeConstants.SUCCESS
            ));
        }

        /// <summary>
        /// Register
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> RegisterAsync([FromBody] RegisterModelView model)
        {
            await _authService.RegisterAsync(model);

            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Register successfully!"
            ));
        }

        /// <summary>
        /// Send phone confirmation code
        /// </summary>
        [HttpPost("send-phone-confirmation")]
        public async Task<IActionResult> SendPhoneConfirmation([FromBody] string phoneNumber)
        {
            await _authService.SendPhoneConfirmationAsync(phoneNumber);

            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Confirmation code sent"
            ));
        }

        /// <summary>
        /// Register with phone number
        /// </summary>
        [HttpPost("register-phone")]
        public async Task<IActionResult> RegisterPhone([FromBody] RegisterPhoneModelView model)
        {
            await _authService.RegisterByPhoneAsync(model);

            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Register successfully!"
            ));
        }

        /// <summary>
        /// Login with Google
        /// </summary>
        [HttpPost("google-login")]
        public async Task<IActionResult> LoginWithGoogle([FromBody] GoogleLoginRequest request)
        {
            try
            {
                var result = await _authService.LoginWithGoogle(request.IdToken);

                return Ok(BaseResponse<AuthResponseModelView>.OkResponse(
                    result,
                    ResponseCodeConstants.SUCCESS
                ));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google Login Error: {ex.Message}");

                return StatusCode(500, new BaseResponse<string>(
                    statusCode: StatusCodeHelper.ServerError,
                    code: ResponseCodeConstants.FAILED,
                    data: ex.Message
                ));
            }
        }

        /// <summary>
        /// Verify user password before sensitive actions
        /// </summary>
        [HttpPost("verify-password")]
        public async Task<IActionResult> VerifyPassword([FromBody] VerifyPasswordModelView model)
        {
            var result = await _authService.VerifyPassword(model);

            return Ok(BaseResponse<bool>.OkResponse(
                result,
                ResponseCodeConstants.SUCCESS
            ));
        }

        /// <summary>
        /// Send email confirmation code
        /// </summary>
        [HttpPost("send-email-confirmation")]
        public async Task<IActionResult> SendEmailConfirmation([FromBody] string email)
        {
            await _authService.SendEmailConfirmationAsync(email);

            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Confirmation email sent"
            ));
        }

        /// <summary>
        /// Send email forgot password link
        /// </summary>
        [HttpPost("send-forgot-password-link")]
        public async Task<IActionResult> SendForgotPasswordLinkAsync([FromBody] string email)
        {
            await _authService.SendForgotPasswordLinkAsync(email);

            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "forgot password link sent"
            ));
        }
    }
}