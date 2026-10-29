using Badminton.API.Configuration;
using Badminton.API.Middleware;
using Badminton.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Repositories.Context;

// A separate test executable: never part of the API image or production startup.
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BADMINTON_TEST_SQLSERVER"))
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BADMINTON_TEST_POSTGRES")))
    throw new InvalidOperationException("Browser tests require an explicit isolated loopback test database server.");

var dist = Path.GetFullPath("../../../../../Frontend/dist", AppContext.BaseDirectory);
if (!File.Exists(Path.Combine(dist, "index.html"))) throw new InvalidOperationException("Build Frontend before browser integration tests.");
await using var fixture = new TestHost(useMigrations: true);
await fixture.RunScopeAsync(async sp =>
{
    var db = sp.GetRequiredService<BadmintonBookingDbContext>();
    var alice = await db.Users.FindAsync(fixture.Alice);
    alice!.PasswordHash = BCrypt.Net.BCrypt.HashPassword("BrowserOnly$2026");
    await db.SaveChangesAsync();
});
await fixture.CountPointsIntoAsync(fixture.Alice, 1000, "browser fixture");
await fixture.AddVoucherAsync("BROWSER", 10, 1, 10000);
// Synthetic club catalogue for the browser flow; this executable only accepts loopback BadmintonTest_*.
fixture.ActAs(fixture.Manager, Badminton.Core.Enums.Role.Manager);
await fixture.RunScopeAsync(async sp => {
    var equipment = sp.GetRequiredService<Contract.Services.Operations.IEquipmentService>();
    await equipment.SaveItemAsync(null, new(fixture.BranchId, "Browser racket", true, 2, 20000, true));
    var coaching = sp.GetRequiredService<Contract.Services.Operations.ICoachingService>();
    var coach = await coaching.SaveCoachAsync(null, new(fixture.BranchId, "Browser coach", "Synthetic class", true));
    var classId = await coaching.AddSlotAsync(coach, new(fixture.HanoiNowUtc.AddHours(2), fixture.HanoiNowUtc.AddHours(3), 4, 100000));
    await ClubCourtFixture.AttachAsync(fixture, classId);
    var tournaments = sp.GetRequiredService<Contract.Services.Operations.ITournamentService>();
    await tournaments.CreateAsync(new(fixture.BranchId, "Browser tournament", 8));
});
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ApplicationName = typeof(Badminton.API.Program).Assembly.GetName().Name,
    EnvironmentName = "Testing"
});
// Clear app/user configuration so local credentials and production overrides cannot leak into this host.
builder.Configuration.Sources.Clear();
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:BadmintonBooking"] = fixture.ConnectionString,
    ["Database:Provider"] = Environment.GetEnvironmentVariable("BADMINTON_TEST_POSTGRES") is null ? "SqlServer" : "PostgreSQL",
    ["JwtSettings:Key"] = new string('b', 64), ["JwtSettings:Issuer"] = "BrowserTests", ["JwtSettings:Audience"] = "BrowserTests",
    ["JwtSettings:ExpireMinutes"] = "10", ["BackgroundJobs:Enabled"] = "false",
    ["DatabaseInitialization:MigrateOnStartup"] = "false", ["DatabaseInitialization:SeedDemoData"] = "false",
    ["Payment:Provider"] = "mock", ["Notify:Channels"] = "log"
});
builder.WebHost.UseUrls("http://127.0.0.1:5099");
var errors = StartupConfiguration.Errors(builder.Configuration, builder.Environment);
if (errors.Length > 0) throw new InvalidOperationException(string.Join("; ", errors));
builder.Services.AddBookingApi(builder.Configuration, builder.Environment);
await using var app = builder.Build();
var files = new PhysicalFileProvider(dist);
app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
app.UseRouting();
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapPost("/__test/shutdown", (HttpContext context, IHostApplicationLifetime lifetime) => {
    context.Response.OnCompleted(() => { lifetime.StopApplication(); return Task.CompletedTask; });
    return Results.Ok();
});
app.MapControllers();
app.MapBookingHealth();
app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files });
await app.RunAsync();
