using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Repositories.Context;

// Separate migration history/model from the historical SQL Server migrations.
public sealed class PostgresBookingDbContext(DbContextOptions<PostgresBookingDbContext> options)
    : BadmintonBookingDbContext(options);

public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresBookingDbContext>
{
    public PostgresBookingDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<PostgresBookingDbContext>().UseNpgsql(
            Environment.GetEnvironmentVariable("ConnectionStrings__BadmintonBooking")
            ?? "Host=localhost;Database=BadmintonDesignTime;Username=postgres;Password=design-time-only")
        .Options);
}
