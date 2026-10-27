using Serilog;
using Serilog.Context;

namespace Badminton.API.Middleware
{
    public sealed class RequestIdMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestIdMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var inbound = context.Request.Headers["X-Request-Id"].FirstOrDefault();
            var requestId = string.IsNullOrWhiteSpace(inbound)
                ? Guid.NewGuid().ToString("N")
                : inbound;
            context.Response.Headers["X-Request-Id"] = requestId;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var p1 = LogContext.PushProperty("RequestId", requestId);
            var p2 = LogContext.PushProperty("HttpMethod", context.Request.Method);
            var logPath = RequestLogPath.Sanitized(context.Request.Path);
            var p3 = LogContext.PushProperty("HttpPath", logPath);
            try
            {
                await _next(context);
            }
            finally
            {
                sw.Stop();
                p1.Dispose(); p2.Dispose(); p3.Dispose();

                Log.Information(
                    "HTTP {Status} {Method} {Path} {ElapsedMs}ms",
                    context.Response.StatusCode,
                    context.Request.Method,
                    logPath,
                    sw.ElapsedMilliseconds);
            }
        }
    }
}
