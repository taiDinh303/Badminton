using Contract.Repositories.UnitOfWork;
using Contract.Services.Auth;
using Contract.Services.Booking;
using Contract.Services.Court;
using Contract.Services.CourtType;
using Contract.Services.Payment;
using Contract.Services.TimeSlot;
using M.Core.Domain;
using Microsoft.Extensions.DependencyInjection;
using Repositories.UnitOfWork;
using Services;
using Services.Pricing;

namespace Services.DI
{
    /// <summary>
    /// The SERVER layer calls <c>AddBookingServices</c> in Program.cs.
    /// This is where encapsulation meets composition: the concrete class for
    /// every contract is bound here, so nothing outside DI knows the types.
    /// </summary>
    public static class ServiceRegistration
    {
        public static IServiceCollection AddBookingServices(this IServiceCollection services)
        {
            // --- Unit of Work (transaction boundary) ---
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // --- Payment strategies (POLYMORPHISM) ---
            services.AddScoped<IPaymentStrategy, CashPaymentStrategy>();
            services.AddScoped<IPaymentStrategy, BankTransferPaymentStrategy>();
            services.AddScoped<IPaymentStrategy, QRCodePaymentStrategy>();
            services.AddScoped<IPaymentStrategyFactory, PaymentStrategyFactory>();

            // --- Fee / pricing policies (POLYMORPHISM) ---
            services.AddSingleton<IFeePolicyFactory, FeePolicyFactory>();

            // --- Security (JWT) ---
            services.AddScoped<JwtGenerator>();

            // --- Concrete services (INHERITANCE: all extend BaseService) ---
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ICourtTypeService, CourtTypeService>();
            services.AddScoped<ICourtService, CourtService>();
            services.AddScoped<ITimeSlotService, TimeSlotService>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IPaymentService, PaymentService>();

            return services;
        }
    }
}
