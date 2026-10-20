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
        services.AddDbContext<BadmintonBookingDbContext>(options => options.UseSqlServer(connection));
        services.AddScoped<IUnitOfWork, Repositories.UnitOfWork.UnitOfWork>();
        return services;
    }
}
