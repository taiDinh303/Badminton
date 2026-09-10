using Contract.Repositories.Entity;
using Contract.Services.Interface;
using Core.Base;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repositories.Context;
using Services;
using Services.Infrastructure;
using Services.Notifications;
using Services.Service;
using System.Text;


namespace API
{
    public static class DependencyInjection
    {
        public static void AddConfig(this IServiceCollection services, IConfiguration configuration)
        {
            services.ConfigRoute();
            services.AddDatabase(configuration);
            services.AddIdentity();
            services.AddInfrastructure(configuration);
            services.AddServices();
            services.AddJwtAuthentication(configuration);
            services.AddSwaggerConfig();
            services.AddHttpContextAccessor();
            //services.AddGoogleAuthentication(configuration);
            services.AddMemoryCache();

            //services.AddSwaggerGen(options =>
            //{
            //    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
            //});

        }
        public static void ConfigRoute(this IServiceCollection services)
        {
            services.Configure<RouteOptions>(options =>
            {
                options.LowercaseUrls = true;
            });
        }
        public static void AddDatabase(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<DatabaseContext>(options =>
            {
                //options.UseLazyLoadingProxies().UseSqlServer(configuration.GetConnectionString("MyCnn"));
                options.UseSqlServer(configuration.GetConnectionString("MyCnn"));

            });
        }

        public static void AddIdentity(this IServiceCollection services)
        {
            services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;

                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                options.User.RequireUniqueEmail = false;
            })
             .AddEntityFrameworkStores<DatabaseContext>()
             .AddDefaultTokenProviders();
        }
        public static void AddServices(this IServiceCollection services)
        {
            services
                .AddScoped<IAuthService, AuthService>()
                .AddScoped<IUserService, UserService>()
                .AddScoped<IRoleService, RoleService>()
                .AddScoped<IUserRoleService, UserRoleService>()
                .AddScoped<IUserInfoService, UserInfoService>()
                .AddScoped<IUserLoginService, UserLoginService>()
                .AddScoped<EmailTemplateRenderer>()
                .AddScoped<IEmailSender, SmtpEmailSender>()
                .AddScoped<IEmailService, EmailService>()
                .AddScoped<IOtpService, OtpService>()


                .AddScoped<ISmsSender, SmsSender>()


                .AddHttpContextAccessor();
        }

        //JWT
        public static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var secret = configuration.GetValue<string>("Jwtsettings:Key");
            var key = Encoding.UTF8.GetBytes(secret ?? throw new InvalidOperationException("JWT Key not found"));


            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwtsettings:Issuer"],

                    ValidateAudience = true,
                    ValidAudience = configuration["Jwtsettings:Audience"],

                    ValidateLifetime = true,

                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuerSigningKey = true
                };


                // ==========================
                // Handle 401 / 403 Response
                // ==========================
                options.Events = new JwtBearerEvents
                {
                    // Không có token hoặc token sai
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";


                        var response = new
                        {
                            statusCode = 401,
                            code = ResponseCodeConstants.UNAUTHORIZED,
                            message = "You are not authenticated",
                            data = (object?)null
                        };


                        await context.Response.WriteAsJsonAsync(response);
                    },


                    // Có token nhưng không đủ quyền Role
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";


                        var response = new
                        {
                            statusCode = 403,
                            code = ResponseCodeConstants.FORBIDDEN,
                            message = "You do not have permission to access this resource",
                            data = (object?)null
                        };


                        await context.Response.WriteAsJsonAsync(response);
                    }
                };
            });
        }
        //Author
        public static void AddSwaggerConfig(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });

                // Cấu hình để Swagger hỗ trợ DateOnly
                c.MapType<DateOnly>(() => new OpenApiSchema
                {
                    Type = "string",
                    Format = "date", // Đảm bảo Swagger hiểu rằng đây là định dạng ngày
                });

                // Cấu hình Authorization
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter your token"
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
                        new string[] {}
                    }
                });
            });
        }

        //public static void AddGoogleAuthentication(this IServiceCollection services, IConfiguration configuration)
        //{
        //    var clientId = configuration["Authentication:Google:ClientId"];
        //    var clientSecret = configuration["Authentication:Google:ClientSecret"];

        //    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        //        throw new InvalidOperationException("Google Authentication configuration is missing (ClientId or ClientSecret).");

        //    services.AddAuthentication()
        //        .AddGoogle(options =>
        //        {
        //            options.ClientId = clientId;
        //            options.ClientSecret = clientSecret;
        //        });
        //}




    }
}
