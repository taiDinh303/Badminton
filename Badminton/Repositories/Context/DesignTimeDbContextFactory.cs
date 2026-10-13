using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Repositories.Context
{
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
