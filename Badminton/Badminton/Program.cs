using M.Core.Store;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repositories.Context;
using Services.DI;
using System.Text;

namespace BadmintonBE.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ---------- DB ----------
            builder.Services.AddDbContext<BadmintonBookingDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("BadmintonBooking")
                    ?? builder.Configuration.GetConnectionString("DefaultConnection")));

            // ---------- Auth (JWT) ----------
            var jwt = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
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

            // ---------- Business services (UoW + strategies + fee policies) ----------
            builder.Services.AddBookingServices();
            builder.Services.AddHttpContextAccessor();

            // ---------- MVC + Swagger ----------
            builder.Services.AddControllers();

            // Read/write enums as their string names (e.g. "BankTransfer",
            // "Paid") instead of numbers, so the API matches the DB's
            // string-typed enum columns. Without this, "method":"BankTransfer"
            // would fail to bind (default expects 0/1/2).
            builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(opt =>
                opt.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter()));
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Badminton Booking API",
                    Version = "v1",
                    Description = "Court booking backend (4-layer: SERVER / SERVICES / REPOSITORY / CORE)."
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

            // Migrate + seed on startup (dev convenience).
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BadmintonBookingDbContext>();
                // Migrate() creates the database (if missing) AND applies the
                // InitialCreate migration in one clean pass. (Using both
                // EnsureCreated + Migrate double-creates the tables, so we
                // stick to Migrate only.)
                db.Database.Migrate();
                SeedData.Seed(db);
            }

            // Exception handling FIRST, so it wraps everything after it.
            app.UseMiddleware<BadmintonBE.API.Middleware.ExceptionMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
