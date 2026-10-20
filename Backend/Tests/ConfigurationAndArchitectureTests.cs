using Badminton.API.Configuration;
using Contract.Services.Booking;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Services;
using Xunit;

namespace Badminton.Tests;

public class ConfigurationAndArchitectureTests
{
    private static Dictionary<string, string?> Valid() => new()
    {
        ["ConnectionStrings:BadmintonBooking"] = "Server=127.0.0.1;Database=BadmintonConfigurationTest;Integrated Security=true;Encrypt=true;",
        ["JwtSettings:Key"] = new string('x', 64),
        ["JwtSettings:Issuer"] = "BadmintonBooking",
        ["JwtSettings:Audience"] = "BadmintonBookingClient",
        ["JwtSettings:ExpireMinutes"] = "60",
        ["Cors:AllowedOrigins:0"] = "https://booking.example.invalid",
        ["Payment:Provider"] = "real",
        ["Payment:Momo:AppId"] = "synthetic",
        ["Payment:Momo:SecretKey"] = "synthetic",
        ["Payment:Momo:Endpoint"] = "https://momo.example.invalid",
        ["Payment:Vnpay:MerchantCode"] = "synthetic",
        ["Payment:Vnpay:SecretKey"] = "synthetic",
        ["Payment:Vnpay:Endpoint"] = "https://vnpay.example.invalid",
        ["Payment:Zalopay:AppId"] = "synthetic",
        ["Payment:Zalopay:ApiKey"] = "synthetic",
        ["Payment:Zalopay:ApiSecret"] = "synthetic",
        ["Payment:Zalopay:Endpoint"] = "https://zalopay.example.invalid",
    };

    private static string[] Check(Dictionary<string, string?> values, string environment = "Production") =>
        StartupConfiguration.Errors(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), new EnvironmentStub(environment));

    [Fact]
    public void Production_configuration_can_be_checked_without_a_database_or_external_service() => Assert.Empty(Check(Valid()));

    [Theory]
    [InlineData("Database:Provider", "PostgreSQL")]
    [InlineData("JwtSettings:Key", "short")]
    [InlineData("JwtSettings:ExpireMinutes", "0")]
    [InlineData("JwtSettings:Audience", "")]
    [InlineData("Cors:AllowedOrigins:0", "http://booking.example.invalid")]
    [InlineData("Cors:AllowedOrigins:0", "https://booking.example.invalid/path")]
    [InlineData("Payment:Provider", "mock")]
    [InlineData("Payment:Momo:SecretKey", "")]
    [InlineData("DatabaseInitialization:MigrateOnStartup", "true")]
    [InlineData("DatabaseInitialization:SeedDemoData", "true")]
    [InlineData("Seed:AllowInProduction", "true")]
    [InlineData("BackgroundJobs:Enabled", "not-a-bool")]
    [InlineData("Notify:Channels", "email")]
    public void Invalid_or_unsafe_setting_fails_closed(string key, string value)
    {
        var values = Valid();
        values[key] = value;
        Assert.NotEmpty(Check(values));
    }

    [Fact]
    public void Configuration_errors_do_not_echo_connection_credentials()
    {
        var values = Valid();
        values["ConnectionStrings:BadmintonBooking"] = "secret-that-must-not-appear";
        Assert.DoesNotContain("secret-that-must-not-appear", string.Join(" ", Check(values)));
    }

    [Fact]
    public void Cors_supports_legacy_comma_separated_configuration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = "https://one.example.invalid, https://two.example.invalid",
        }).Build();
        Assert.Equal(new[] { "https://one.example.invalid", "https://two.example.invalid" }, StartupConfiguration.Origins(configuration));
    }

    [Fact]
    public void Development_keeps_mock_payment_and_local_initialization_available()
    {
        var values = Valid();
        values["Payment:Provider"] = "mock";
        values["DatabaseInitialization:MigrateOnStartup"] = "true";
        Assert.Empty(Check(values, "Development"));
    }

    [Fact]
    public void Contracts_and_business_do_not_reference_persistence_implementations()
    {
        foreach (var assembly in new[] { typeof(IBookingService).Assembly, typeof(BookingService).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), reference =>
                reference.Name == "Repositories" || reference.Name?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true
                || reference.Name == "Microsoft.Data.SqlClient"
                || reference.Name?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(typeof(Badminton.Core.Base.UniqueConstraintException).Assembly.GetReferencedAssemblies(),
            reference => reference.Name?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true);
    }

    private sealed class EnvironmentStub(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Badminton.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
