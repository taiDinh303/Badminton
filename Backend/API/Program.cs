using Badminton.Core.Domain;
using Badminton.Core.Store;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Repositories.Context;
using Serilog;
using Services.DI;
using Badminton.API.Jobs;
using Badminton.API.Middleware;
using System.Text;

namespace Badminton.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var logFileDir =
                builder.Configuration["Logging:FileDirectory"]
                ?? Path.Combine(AppContext.BaseDirectory, "logs");
            builder.Host.UseSerilog((ctx, cfg) =>
                cfg.ReadFrom.Configuration(ctx.Configuration)
                   .Enrich.FromLogContext()
                   .WriteTo.Console(outputTemplate:
                       "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                   .WriteTo.File(
                       Path.Combine(logFileDir, "badminton-.json"),
                       rollingInterval: RollingInterval.Day,
                       retainedFileCountLimit: 14,
                       outputTemplate:
                       "{Timestamp:yyyy-MM-dd HH:mm:ss.fff z} [{Level:u3}] {RequestId} {SourceContext} | {Message:lj} | {Exception}"));

            builder.Services.AddSingleton<IMetricsReporter, PrometheusMetricsReporter>();

            builder.Services.AddDbContext<BadmintonBookingDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("BadmintonBooking")
                    ?? builder.Configuration.GetConnectionString("DefaultConnection")));

            var jwt = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
            if (string.IsNullOrWhiteSpace(jwt.Key))
                throw new InvalidOperationException(
                    "JwtSettings:Key must be provided (environment variable " +
                    "JwtSettings__Key). Refusing to start with no signing key.");
            if (jwt.Key.Length < 32 && !builder.Environment.IsDevelopment())
                throw new InvalidOperationException(
                    "JwtSettings:Key must be at least 32 characters in production.");
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    o.MapInboundClaims = false;
                    o.SaveToken = true;
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwt.Key)),
                        ValidIssuer = jwt.Issuer,
                        ValidAudience = jwt.Audience,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                });
            builder.Services.AddAuthorization();

            // ── CORS: allow the React SPA (Vercel) to call the API. ─────
            var corsOrigins = builder.Configuration["Cors:AllowedOrigins"]
                ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();
            builder.Services.AddCors(o =>
            {
                o.AddDefaultPolicy(p =>
                {
                    p.AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Request-Id");
                    if (corsOrigins.Length > 0)
                        p.WithOrigins(corsOrigins);
                    else
                        p.AllowAnyOrigin();
                });
            });

            builder.Services.AddRateLimiter(o =>
            {
                o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                o.AddFixedWindowLimiter("public", w =>
                {
                    w.PermitLimit = 100;
                    w.Window = TimeSpan.FromSeconds(10);
                    w.QueueLimit = 0;
                });
                o.AddFixedWindowLimiter("auth", w =>
                {
                    w.PermitLimit = 5;
                    w.Window = TimeSpan.FromSeconds(60);
                    w.QueueLimit = 0;
                });
            });

            builder.Services.AddBookingServices(builder.Configuration);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddMemoryCache();

            builder.Services.AddSingleton<JobLock>();
            builder.Services.AddSingleton<JobHeartbeatRegistry>();
            builder.Services.AddHostedService<PendingBookingExpiryJob>();
            builder.Services.AddHostedService<AutoCompleteJob>();
            builder.Services.AddHostedService<VoucherExpiryJob>();
            builder.Services.AddHostedService<NoShowResetJob>();
            // FEAT 2 — waitlist promotion (locked + heartbeat, every 2 min).
            builder.Services.AddHostedService<WaitlistPromotionJob>();

            builder.Services.AddControllers();
            builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(opt =>
                opt.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter()));
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Badminton Booking API v1",
                    Version = "v1",
                    Description = "Court booking (docs 01–05): branches, courts, " +
                                  "slots, booking, payment, voucher, points, review."
                });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BadmintonBookingDbContext>();
                db.Database.Migrate();

                bool allowSeedInProd =
                    builder.Configuration.GetValue("Seed:AllowInProduction", false);
                if (app.Environment.IsDevelopment())
                {
                    SeedData.Seed(db);
                }
                else if (allowSeedInProd)
                {
                    Log.Warning(
                        "Seed:AllowInProduction=true — seeding demo data in " +
                        "a NON-Development environment. This should only ever " +
                        "be done in an isolated CI sandbox, never on prod.");
                    SeedData.Seed(db);
                }
            }

            app.UseMiddleware<RequestIdMiddleware>();
            app.UseHttpMetrics();
            app.UseMiddleware<ExceptionMiddleware>();
            app.UseCors();
            app.UseRateLimiter();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.MapMetrics("/metrics");

            app.MapGet("/health", () => Results.Ok(new
            {
                status = "ok",
                ts = DateTime.UtcNow.ToString("o")
            }));

            app.MapGet("/ready", async (BadmintonBookingDbContext db,
                JobHeartbeatRegistry heartbeat) =>
            {
                bool dbOk;
                try { dbOk = await db.Database.CanConnectAsync(); }
                catch { dbOk = false; }

                var now = DateTime.UtcNow;
                var jobs = new Dictionary<string, object>
                {
                    ["PendingBookingExpiryJob"] = JobHealth(now,
                        heartbeat.Last("PendingBookingExpiryJob"), TimeSpan.FromMinutes(2)),
                    ["AutoCompleteJob"] = JobHealth(now,
                        heartbeat.Last("AutoCompleteJob"), TimeSpan.FromMinutes(2)),
                    ["VoucherExpiryJob"] = JobHealth(now,
                        heartbeat.Last("VoucherExpiryJob"), TimeSpan.FromHours(1)),
                    ["NoShowResetJob"] = JobHealth(now,
                        heartbeat.Last("NoShowResetJob"), TimeSpan.FromHours(1))
                };
                bool jobsOk = jobs.Values.All(v => v is true);

                var payload = new
                {
                    status = dbOk && jobsOk ? "ready" : "not_ready",
                    db = dbOk ? "ok" : "unreachable",
                    jobs
                };
                Microsoft.AspNetCore.Http.IResult result = dbOk && jobsOk
                    ? Results.Ok(payload)
                    : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
                return result;
            });

            app.Run();
        }

        private static object JobHealth(DateTime nowUtc, DateTime? lastUtc, TimeSpan maxAge)
        {
            if (lastUtc is null)
                return "pending";
            var age = nowUtc - lastUtc.Value;
            return age <= maxAge
                ? true
                : $"stale ({(int)age.TotalSeconds}s old, max {maxAge.TotalSeconds}s)";
        }
    }
}
