using System.Net;
using Core.Base;

namespace API.Middleware
{
    public class ExceptionMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (BaseException.ErrorException ex)
            {
                await HandleExceptionAsync(context, ex.StatusCode, ex.ErrorDetail.ErrorCode, ex.ErrorDetail.ErrorMessage?.ToString());
            }
            catch (Exception)
            {
                await HandleExceptionAsync(context, (int)HttpStatusCode.InternalServerError,
                    ResponseCodeConstants.INTERNAL_SERVER_ERROR, "An unexpected error occurred");
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, int statusCode, string? errorCode, object? errorMessage)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var response = new
            {
                statusCode,
                code = errorCode,
                message = errorMessage,
                data = (object?)null
            };

            return context.Response.WriteAsJsonAsync(response);
        }
    }
    public static class ExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseExceptionMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionMiddleware>();
        }
    }
}
