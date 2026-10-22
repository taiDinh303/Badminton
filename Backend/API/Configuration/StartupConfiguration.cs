using System.Text;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Badminton.API.Configuration;

public static class StartupConfiguration
{
    public static string[] Origins(IConfiguration configuration)
    {
        var section = configuration.GetSection("Cors:AllowedOrigins");
        return section.GetChildren().Any()
            ? section.Get<string[]>() ?? []
            : (section.Value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static string[] Errors(IConfiguration configuration, IHostEnvironment environment)
    {
        var errors = new List<string>();
        var production = !environment.IsDevelopment() && !environment.IsEnvironment("Testing");
        var databaseProvider = configuration["Database:Provider"] ?? "SqlServer";
        var postgres = databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase);
        if (!postgres && !databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
            errors.Add("Database:Provider must be SqlServer or PostgreSQL.");
        var connection = configuration.GetConnectionString("BadmintonBooking")
            ?? configuration.GetConnectionString("DefaultConnection");
        try
        {
            if (postgres)
            {
                var pg = new NpgsqlConnectionStringBuilder(connection);
                if (string.IsNullOrWhiteSpace(pg.Host) || string.IsNullOrWhiteSpace(pg.Database) || string.IsNullOrWhiteSpace(pg.Username))
                    errors.Add("ConnectionStrings:BadmintonBooking requires host, database and username.");
                if (production && pg.SslMode is not (SslMode.VerifyFull or SslMode.VerifyCA))
                    errors.Add("ConnectionStrings:BadmintonBooking requires verified TLS outside Development/Testing.");
                if (pg.Port == 6543)
                    errors.Add("ConnectionStrings:BadmintonBooking requires direct or session pooling (port 5432) for job locks.");
            }
            else
            {
            var sql = new SqlConnectionStringBuilder(connection);
            if (string.IsNullOrWhiteSpace(sql.DataSource) || string.IsNullOrWhiteSpace(sql.InitialCatalog))
                errors.Add("ConnectionStrings:BadmintonBooking requires a server and database.");
            if (production && sql.DataSource.Contains("localdb", StringComparison.OrdinalIgnoreCase))
                errors.Add("ConnectionStrings:BadmintonBooking cannot use LocalDB outside Development/Testing.");
            }
        }
        catch (ArgumentException) { errors.Add("ConnectionStrings:BadmintonBooking has invalid syntax."); }

        var key = configuration["JwtSettings:Key"] ?? "";
        if (Encoding.UTF8.GetByteCount(key) < 32 || key.Contains("change-me", StringComparison.OrdinalIgnoreCase)
            || key.Contains("<", StringComparison.Ordinal))
            errors.Add("JwtSettings:Key requires a private signing key of at least 32 UTF-8 bytes.");
        foreach (var name in new[] { "Issuer", "Audience" })
            if (string.IsNullOrWhiteSpace(configuration[$"JwtSettings:{name}"])) errors.Add($"JwtSettings:{name} is required.");
        if (!int.TryParse(configuration["JwtSettings:ExpireMinutes"], out var ttl) || ttl <= 0 || ttl > 1440)
            errors.Add("JwtSettings:ExpireMinutes must be between 1 and 1440.");

        var origins = Origins(configuration);
        if (production && origins.Length == 0) errors.Add("Cors:AllowedOrigins requires explicit HTTPS origins.");
        foreach (var origin in origins)
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0
                || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0
                || (production ? uri.Scheme != "https" : uri.Scheme is not ("http" or "https")))
                errors.Add("Cors:AllowedOrigins contains an invalid origin.");

        foreach (var flag in new[] { "DatabaseInitialization:MigrateOnStartup", "DatabaseInitialization:SeedDemoData", "Seed:AllowInProduction", "BackgroundJobs:Enabled", "Swagger:Enabled" })
            if (configuration[flag] is { } value && !bool.TryParse(value, out _)) errors.Add($"{flag} must be true or false.");
        if (production && (Enabled(configuration, "DatabaseInitialization:MigrateOnStartup")
            || Enabled(configuration, "DatabaseInitialization:SeedDemoData")
            || Enabled(configuration, "Seed:AllowInProduction")))
            errors.Add("DatabaseInitialization and Seed:AllowInProduction must not enable migration/demo seeding outside Development/Testing.");

        var provider = (configuration["Payment:Provider"] ?? "mock").Trim().ToLowerInvariant();
        if (provider is not ("mock" or "real" or "onsite")) errors.Add("Payment:Provider must be mock, real or onsite.");
        if (production && provider == "mock") errors.Add("Payment:Provider must be real or onsite outside Development/Testing.");
        if (provider == "real")
        {
            foreach (var path in new[] { "Payment:Momo:AppId", "Payment:Momo:SecretKey", "Payment:Vnpay:MerchantCode", "Payment:Vnpay:SecretKey", "Payment:Zalopay:AppId", "Payment:Zalopay:ApiKey", "Payment:Zalopay:ApiSecret" })
                if (string.IsNullOrWhiteSpace(configuration[path])) errors.Add($"{path} is required for real payment adapters.");
            foreach (var path in new[] { "Payment:Momo:Endpoint", "Payment:Vnpay:Endpoint", "Payment:Zalopay:Endpoint" })
                if (!IsHttps(configuration[path])) errors.Add($"{path} requires an absolute HTTPS URL without credentials.");
        }
        var channels = (configuration["Notify:Channels"] ?? "log").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (channels.Length == 0) errors.Add("Notify:Channels requires at least one channel.");
        foreach (var channel in channels)
        {
            if (channel == "log") continue;
            var prefix = channel switch { "email" => "Email", "sms" => "Sms", "zalo" => "Zalo", _ => null };
            if (prefix is null) { errors.Add("Notify:Channels contains an unsupported channel."); continue; }
            if (!IsHttps(configuration[$"Notify:{prefix}Endpoint"])) errors.Add($"Notify:{prefix}Endpoint requires an absolute HTTPS URL.");
            var secret = prefix == "Zalo" ? "ZaloToken" : prefix + "Key";
            if (string.IsNullOrWhiteSpace(configuration[$"Notify:{secret}"])) errors.Add($"Notify:{secret} is required for the enabled channel.");
        }
        foreach (var path in new[] { "Membership:MultiplierSilver", "Membership:MultiplierGold" })
            if (configuration[path] is { } multiplier && (!decimal.TryParse(multiplier, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)) errors.Add($"{path} must be positive.");
        return errors.Distinct().ToArray();
    }

    private static bool Enabled(IConfiguration configuration, string key) => bool.TryParse(configuration[key], out var enabled) && enabled;

    private static bool IsHttps(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
}
