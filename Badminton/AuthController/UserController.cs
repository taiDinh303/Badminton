using Contract.Services.Interface;
using Core.Base;
using Core.Store;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelViews.UserInfoModelView;
using ModelViews.UserModelView;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(Roles = "Admin,User")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        #region User


        /// <summary>
        /// Retrieves all users with pagination
        /// </summary>
        [HttpGet("get-all")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAll(int pageNumber = 1, int pageSize = 10)
        {
            BasePaginatedList<UserResponseModelView> result = await _userService.GetAllAsync(pageNumber, pageSize);
            return Ok(new BaseResponse<BasePaginatedList<UserResponseModelView>>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: result
            ));
        }
        /// <summary>
        /// Retrieves a user by ID
        /// </summary>
        [HttpGet("get-by-id/{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            UserResponseModelView result = await _userService.GetByIdAsync(id);

            return Ok(new BaseResponse<UserResponseModelView>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: result
            ));
        }
        /// <summary>
        /// Creates a new user
        /// </summary>
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateUserModelView model)
        {
            await _userService.CreateAsync(model);
            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Create user successfully!"
            ));
        }

        /// <summary>
        /// Updates user information
        /// </summary>
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromBody] UpdateUserModelView model)
        {
            await _userService.UpdateAsync(model);

            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "Update user successfully!"
            ));
        }


        /// <summary>
        /// Soft deletes a user
        /// </summary>
        [HttpDelete("soft-delete/{id}")]
        public async Task<IActionResult> SoftDelete(Guid id)
        {
            await _userService.SoftDeleteAsync(id);

            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "User soft deleted successfully!"
            ));
        }

        /// <summary>
        /// Permanently deletes a user
        /// </summary>
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _userService.DeleteAsync(id);

            return Ok(new BaseResponse<string>(
                statusCode: StatusCodeHelper.OK,
                code: ResponseCodeConstants.SUCCESS,
                data: "User deleted successfully!"
            ));
        }
        #endregion



        #region Email

        /// <summary>
        /// Sends a confirmation email to change the user's email address
        /// </summary>
        [HttpPost("send-change-email")]
        public async Task<IActionResult> SendChangeEmail([FromBody] UpdateEmailModelView request)
        {
            await _userService.SendChangeEmailAsync(request);

            return Ok(new BaseResponse<string>(
                 statusCode: StatusCodeHelper.OK,
                 code: ResponseCodeConstants.SUCCESS,
                 data: "Confirmation email sent"
            ));
        }
        /// <summary>
        /// Confirms the user's email address change
        /// </summary>
        [HttpGet("confirm-change-email")]
        public async Task<IActionResult> ConfirmChangeEmail(Guid userId, string token, string newEmail)
        {
            await _userService.ConfirmChangeEmailAsync(userId, token, newEmail);

            return Ok(new BaseResponse<string>(
                 statusCode: StatusCodeHelper.OK,
                 code: ResponseCodeConstants.SUCCESS,
                 data: "Email changed successfully"
            ));
        }

        #endregion

        #region Password Management
        /// <summary>
        /// Sends an email to set a password for a Google account
        /// </summary>
        [HttpPost("send-set-password-email/{userId}")]
        public async Task<IActionResult> SendSetPasswordEmail(Guid userId)
        {
            await _userService.SendSetPasswordLinkAsync(userId);

            return Ok(new BaseResponse<string>(
                StatusCodeHelper.OK,
                ResponseCodeConstants.SUCCESS,
                "Email tạo mật khẩu đã được gửi!"
            ));
        }

        /// <summary>
        /// Sets a new password using the email confirmation token
        /// </summary>
        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest model)
        {
            await _userService.SetPasswordAsync(model.UserId, model.Token, model.NewPassword);

            return Ok(new BaseResponse<string>(
                StatusCodeHelper.OK,
                ResponseCodeConstants.SUCCESS,
                "Tạo mật khẩu thành công!"
            ));
        }

        /// <summary>
        /// Changes the user's password
        /// </summary>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModelView model)
        {
            await _userService.ChangePasswordAsync(model.UserId, model.CurrentPassword, model.NewPassword);

            return Ok(new BaseResponse<string>(
                StatusCodeHelper.OK,
                ResponseCodeConstants.SUCCESS,
                "Password changed successfully!"
            ));
        }



        #endregion



    }
}
