using Badminton.Core.Base;
using Badminton.Core.Store;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Badminton.API.Middleware
{
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
                _logger.LogError(ex, "Unhandled on {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                await WriteAsync(context, (int)StatusCodeHelper.ServerError,
                    ResponseCodeConstants.INTERNAL_SERVER_ERROR,
                    "Có lỗi xảy ra, vui lòng thử lại.");
            }
        }

        private static async Task WriteAsync(
            HttpContext context, int httpStatus, string code, string message)
        {
            context.Response.StatusCode = httpStatus;
            context.Response.ContentType = "application/json";
            var payload = new BaseResponse<string>(
                (StatusCodeHelper)httpStatus, code, message);
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
