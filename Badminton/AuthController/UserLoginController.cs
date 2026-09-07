//using Contract.Services.Interface;
//using Core.Base;
//using Core.Store;
//using Microsoft.AspNetCore.Mvc;

//namespace API.Controllers
//{
//    [Route("api/[controller]")]
//    [ApiController]
//    // No Authorize
//    public class UserLoginController : ControllerBase
//    {
//        private readonly IUserLoginService _userLoginService;

//        public UserLoginController(IUserLoginService userLoginService)
//        {
//            _userLoginService = userLoginService;
//        }

//        /// <summary>
//        /// Check if the user has linked a Google account (or another provider)
//        /// </summary>
//        [HttpGet("is-connected")]
//        public async Task<IActionResult> IsConnected(Guid userId, string provider = "Google")
//        {
//            bool isConnected = await _userLoginService.IsConnectedAsync(userId, provider);

//            return Ok(new BaseResponse<bool>(
//                statusCode: StatusCodeHelper.OK,
//                code: ResponseCodeConstants.SUCCESS,
//                data: isConnected
//            ));
//        }

//        /// <summary>
//        ///  Get all linked login providers for the user
//        /// </summary>
//        [HttpGet("get-all")]
//        public async Task<IActionResult> GetAll(Guid userId)
//        {
//            var result = await _userLoginService.GetAllByUserAsync(userId);

//            return Ok(new BaseResponse<object>(
//                statusCode: StatusCodeHelper.OK,
//                code: ResponseCodeConstants.SUCCESS,
//                data: result
//            ));
//        }

//        /// <summary>
//        /// Link a Google account (or another provider)
//        /// </summary>
//        [HttpPost("connect")]
//        public async Task<IActionResult> Connect(Guid userId, string provider, string providerKey, string? displayName = null)
//        {
//            await _userLoginService.ConnectAsync(userId, provider, providerKey, displayName);

//            return Ok(new BaseResponse<string>(
//                statusCode: StatusCodeHelper.OK,
//                code: ResponseCodeConstants.SUCCESS,
//                data: $"{provider} connected successfully!"
//            ));
//        }

//        /// <summary>
//        /// Unlink a connected account (e.g., Google)
//        /// </summary>
//        [HttpDelete("disconnect")]
//        public async Task<IActionResult> Disconnect(Guid userId, string provider = "Google")
//        {
//            await _userLoginService.DisconnectAsync(userId, provider);

//            return Ok(new BaseResponse<string>(
//                statusCode: StatusCodeHelper.OK,
//                code: ResponseCodeConstants.SUCCESS,
//                data: $"{provider} disconnected successfully!"
//            ));
//        }
//    }
//}
