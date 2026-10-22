using Badminton.API.Jobs;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Badminton.API.Configuration;

public static class HealthEndpoints
{
    public static void MapBookingHealth(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", ts = DateTime.UtcNow }));
        app.MapGet("/ready", async (BadmintonBookingDbContext db, JobHeartbeatRegistry heartbeat, CancellationToken ct) =>
        {
            bool dbOk;
            try { dbOk = await db.Database.CanConnectAsync(ct); }
            catch { dbOk = false; }
            var jobs = new Dictionary<string, string>();
            if (app.Configuration.GetValue("BackgroundJobs:Enabled", true))
                foreach (var (name, maxAge) in new[]
                {
                    ("PendingBookingExpiryJob", TimeSpan.FromMinutes(3)),
                    ("AutoCompleteJob", TimeSpan.FromMinutes(3)),
                    ("VoucherExpiryJob", TimeSpan.FromHours(2)),
                    ("NoShowResetJob", TimeSpan.FromHours(2)),
                    ("WaitlistPromotionJob", TimeSpan.FromMinutes(5))
                })
                    jobs[name] = heartbeat.Last(name) is { } last
                        ? DateTime.UtcNow - last <= maxAge ? "ok" : "stale" : "pending";
            var ready = dbOk && jobs.Values.All(value => value == "ok");
            return Results.Json(new { status = ready ? "ready" : "not_ready", db = dbOk ? "ok" : "unreachable", jobs },
                statusCode: ready ? 200 : 503);
        });
    }
}
