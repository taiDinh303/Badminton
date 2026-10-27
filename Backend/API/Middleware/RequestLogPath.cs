namespace Badminton.API.Middleware;

public static class RequestLogPath
{
    public static string Sanitized(PathString path) => path.StartsWithSegments("/api/v1/club/calendar/feed", StringComparison.OrdinalIgnoreCase)
        ? "/api/v1/club/calendar/feed/[redacted].ics"
        : path.Value ?? "";
}
