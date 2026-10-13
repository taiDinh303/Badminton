using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Repositories.Context
{
    /// <summary>
    /// Design-time factory so <c>dotnet ef</c> / <c>Update-Database</c> can create
    /// the context (for migrations / database update) without booting the API.
    /// Connection string matches appsettings.json exactly.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BadmintonBookingDbContext>
    {
        public BadmintonBookingDbContext CreateDbContext(string[] args)
        {
            var conn =
                Environment.GetEnvironmentVariable("BadmintonBooking__ConnectionStrings__BadmintonBooking")
                ?? "Server=(localdb)\\MSSQLLocalDB;Database=BadmintonBooking;Trusted_Connection=True;TrustServerCertificate=True;";

            var options = new DbContextOptionsBuilder<BadmintonBookingDbContext>()
                .UseSqlServer(conn)
                .Options;
            return new BadmintonBookingDbContext(options);
        }
    }
}
