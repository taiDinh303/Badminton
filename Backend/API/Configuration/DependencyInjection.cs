using Badminton.Core.Domain;
using Badminton.Core.Security;
using Badminton.API.Security;
using Badminton.Core.Store;
using Badminton.API.Jobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repositories;
using Services.DI;
using System.Text;
using System.Threading.RateLimiting;

namespace Badminton.API.Configuration;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingApi(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton<IMetricsReporter, PrometheusMetricsReporter>();

        services.AddBookingPersistence(configuration);

        var jwt = configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services
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
        services.AddAuthorization();

        // ── CORS: allow the React SPA (Vercel) to call the API. ─────
        var corsOrigins = StartupConfiguration.Origins(configuration);
        services.AddCors(o =>
        {
            o.AddDefaultPolicy(p =>
            {
                p.AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Request-Id");
                if (corsOrigins.Length > 0)
                    p.WithOrigins(corsOrigins);
                else if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
                    p.AllowAnyOrigin();
            });
        });

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            foreach (var (name, limit, seconds) in new[] { ("public", 100, 10), ("auth", 5, 60), ("contact", 3, 60) })
                o.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit, Window = TimeSpan.FromSeconds(seconds), QueueLimit = 0
                    }));
        });

        services.AddBookingServices(configuration);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddMemoryCache();

        services.AddSingleton<JobLock>();
        services.AddSingleton<JobHeartbeatRegistry>();
        if (configuration.GetValue("BackgroundJobs:Enabled", true))
        {
            services.AddHostedService<PendingBookingExpiryJob>();
            services.AddHostedService<AutoCompleteJob>();
            services.AddHostedService<VoucherExpiryJob>();
            services.AddHostedService<NoShowResetJob>();
            services.AddHostedService<WaitlistPromotionJob>();
        }

        services.AddControllers().ConfigureApiBehaviorOptions(options =>
            options.InvalidModelStateResponseFactory = context => new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new
            {
                data = (object?)null, message = "Dữ liệu yêu cầu không hợp lệ.", statusCode = 400,
                code = "VALIDATION_ERROR", requestId = context.HttpContext.TraceIdentifier
            }));
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(opt =>
            opt.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter()));
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
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

        return services;
    }
}
