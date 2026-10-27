using Badminton.API.Bootstrap;
using Badminton.API.Configuration;
using Badminton.API.Middleware;
using Prometheus;
using Serilog;

namespace Badminton.API;

public partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var errors = StartupConfiguration.Errors(builder.Configuration, builder.Environment);
        if (errors.Length > 0)
        {
            Console.Error.WriteLine("CONFIGURATION_INVALID: " + string.Join(" ", errors));
            return 2;
        }
        if (args.Contains("--check-config", StringComparer.Ordinal))
        {
            Console.WriteLine("CONFIGURATION_OK: no database, migration, seed, worker or external request was performed.");
            return 0;
        }

        var directory = builder.Configuration["Logging:FileDirectory"];
        if (string.IsNullOrWhiteSpace(directory)) directory = Path.Combine(AppContext.BaseDirectory, "logs");
        builder.Host.UseSerilog((context, logger) => logger
            .ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext().WriteTo.Console()
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
            .WriteTo.File(Path.Combine(directory, "badminton-.log"), rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14));
        builder.Services.AddBookingApi(builder.Configuration, builder.Environment);
        var app = builder.Build();
        await app.InitializeDatabaseAsync();

        app.UseMiddleware<RequestIdMiddleware>();
        app.UseHttpMetrics();
        app.UseMiddleware<ExceptionMiddleware>();
        app.UseCors();
        if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();
        app.MapBookingHealth();
        app.MapMetrics("/metrics");
        await app.RunAsync();
        return 0;
    }
}
