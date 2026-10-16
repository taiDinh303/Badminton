using M.Core.Store;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repositories.Context;
using Services.DI;
using BadmintonBE.API.Jobs;
using System.Text;

namespace BadmintonBE.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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
            // In production set Cors:AllowedOrigins (comma-separated) to lock
            // the origin list down; otherwise fall back to any-origin so the
            // dev baseline keeps working out of the box.
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

            // ── Rate-limiter: global + strict auth bucket ─────────────
            builder.Services.AddRateLimiter(o =>
            {
                o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                // Global: 100 req/10s per connection.
                o.AddFixedWindowLimiter("global", w =>
                {
                    w.PermitLimit = 100;
                    w.Window = TimeSpan.FromSeconds(10);
                    w.QueueLimit = 0;
                });
                // Auth: 5 attempts/60s (brute-force guard) — applied per-endpoint
                // via [EnableRateLimiting("auth")] on AuthController.
                o.AddFixedWindowLimiter("auth", w =>
                {
                    w.PermitLimit = 5;
                    w.Window = TimeSpan.FromSeconds(60);
                    w.QueueLimit = 0;
                });
            });

            builder.Services.AddBookingServices();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddMemoryCache();

            builder.Services.AddHostedService<PendingBookingExpiryJob>();
            builder.Services.AddHostedService<AutoCompleteJob>();
            builder.Services.AddHostedService<VoucherExpiryJob>();
            builder.Services.AddHostedService<NoShowResetJob>();

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
                // Seed demo data ONLY in Development. A fresh production DB
                // must NOT be populated with the default admin (Admin@123) and
                // 500 demo customers.
                if (app.Environment.IsDevelopment())
                    SeedData.Seed(db);
            }

            app.UseMiddleware<BadmintonBE.API.Middleware.ExceptionMiddleware>();
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

            app.Run();
        }
    }
}
