using M.Core.Base;
using M.Core.Store;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace BadmintonBE.API.Middleware
{
    /// <summary>
    /// ENCAPSULATION + ABSTRACTION: every domain exception is translated to a
    /// uniform <see cref="BaseResponse{string}"/> in ONE place, so controllers
    /// throw cleanly and never write their own try/catch. Unknown exceptions
    /// collapse to a 500 without leaking internals.
    /// </summary>
    public sealed class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (BaseException ex)
            {
                await WriteAsync(context, ex.StatusCode, ex.Code, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                await WriteAsync(context,
                    (int)StatusCodeHelper.ServerError,
                    ResponseCodeConstants.INTERNAL_SERVER_ERROR,
                    "An unexpected error occurred.");
            }
        }

        private static async Task WriteAsync(
            HttpContext context, int httpStatus, string code, string message)
        {
            context.Response.StatusCode = httpStatus;
            context.Response.ContentType = "application/json";
            var payload = new BaseResponse<string>(
                (StatusCodeHelper)httpStatus, code, message);
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
        }
    }
}
