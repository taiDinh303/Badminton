using M.Core.Base;
using M.Core.Store;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BadmintonBE.API.Controllers
{
    /// <summary>
    /// ABSTRACTION + INHERITANCE: the base every API controller extends. It
    /// unifies response wrapping into <see cref="BaseResponse{T}"/> so the
    /// "shape of every answer" lives in ONE place (ENCAPSULATION). All helper
    /// names are prefixed <c>Resp</c> to never collide with MVC's built-in
    /// <c>Ok(...)</c> (avoiding the classic self-recursion stack overflow).
    /// </summary>
    public abstract class BaseApiController : ControllerBase
    {
        /// <summary>The authenticated caller's name (or "system" when absent).</summary>
        protected string CurrentUserName =>
            User?.Identity?.Name
            ?? User?.FindFirst(ClaimTypes.Name)?.Value
            ?? "system";

        /// <summary>Wraps <c>data</c> in a 200 <see cref="BaseResponse{T}"/>.</summary>
        protected IActionResult Resp<T>(T data, string? message = null) =>
            Ok(new BaseResponse<T>(StatusCodeHelper.OK, ResponseCodeConstants.SUCCESS, data, message));

        /// <summary>Wraps a message-only success.</summary>
        protected IActionResult RespMsg(string? message = "Success") =>
            Ok(new BaseResponse<string>(StatusCodeHelper.OK, ResponseCodeConstants.SUCCESS, message));

        /// <summary>Wraps a 400 bad request.</summary>
        protected IActionResult RespBad<T>(string message) =>
            BadRequest(new BaseResponse<T>(StatusCodeHelper.BadRequest,
                ResponseCodeConstants.BAD_REQUEST, default, message));

        /// <summary>Wraps a 404 (string-typed, no data).</summary>
        protected IActionResult RespNotFound(string message) =>
            NotFound(new BaseResponse<string>(StatusCodeHelper.NotFound,
                ResponseCodeConstants.NOT_FOUND, null, message));
    }
}
