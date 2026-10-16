using Badminton.Core.Base;
using Badminton.Core.Store;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Badminton.API.Controllers
{
    public abstract class BaseApiController : ControllerBase
    {
        protected int CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        protected string CurrentUserName =>
            User?.Identity?.Name
            ?? User?.FindFirst(ClaimTypes.Name)?.Value
            ?? "system";

        protected bool IsStaff =>
            User.IsInRole("Admin") || User.IsInRole("Manager");

        protected bool IsAdmin => User.IsInRole("Admin");

        protected IActionResult Resp<T>(T data, string? message = null) =>
            Ok(new BaseResponse<T>(StatusCodeHelper.OK, ResponseCodeConstants.SUCCESS, data, message));

        protected IActionResult RespMsg(string? message = "Success") =>
            Ok(new BaseResponse<string>(StatusCodeHelper.OK, ResponseCodeConstants.SUCCESS, message));

        protected IActionResult RespNotFound(string message) =>
            NotFound(new BaseResponse<string>(StatusCodeHelper.NotFound,
                ResponseCodeConstants.NOT_FOUND, null, message));
    }
}
