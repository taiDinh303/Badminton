using Contract.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Context;
using Repositories.UnitOfWork;

namespace Repositories;

public static class DependencyInjection
{
    public static IServiceCollection AddBookingPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("BadmintonBooking")
            ?? configuration.GetConnectionString("DefaultConnection");
        if (string.Equals(configuration["Database:Provider"], "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<PostgresBookingDbContext>(options => options.UseNpgsql(connection));
            services.AddScoped<BadmintonBookingDbContext>(sp => sp.GetRequiredService<PostgresBookingDbContext>());
        }
        else services.AddDbContext<BadmintonBookingDbContext>(options => options.UseSqlServer(connection));
        services.AddScoped<IUnitOfWork, Repositories.UnitOfWork.UnitOfWork>();
        services.AddScoped<IOperationsReadStore, Repositories.UnitOfWork.OperationsReadStore>();
        return services;
    }
}
