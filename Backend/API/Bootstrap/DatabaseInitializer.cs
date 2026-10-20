using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Badminton.API.Bootstrap;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var migrate = app.Configuration.GetValue("DatabaseInitialization:MigrateOnStartup", app.Environment.IsDevelopment());
        var seed = app.Configuration.GetValue("DatabaseInitialization:SeedDemoData", app.Environment.IsDevelopment());
        if (!migrate && !seed) return;
        // Validate again at the side-effect boundary, independently of the startup validator.
        if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
            throw new InvalidOperationException("Database initialization is restricted to Development or isolated Testing.");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BadmintonBookingDbContext>();
        if (migrate) await db.Database.MigrateAsync();
        if (seed) SeedData.Seed(db);
    }
}
