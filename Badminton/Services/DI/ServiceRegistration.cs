using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Auth;
using Contract.Services.Booking;
using Contract.Services.Branch;
using Contract.Services.Court;
using Contract.Services.Group;
using Contract.Services.Maintenance;
using Contract.Services.Payment;
using Contract.Services.Points;
using Contract.Services.Pricing;
using Contract.Services.Review;
using Contract.Services.Staff;
using Contract.Services.TimeSlot;
using Contract.Services.Voucher;
using M.Core.Domain;
using M.Core.Enums;
using Microsoft.Extensions.DependencyInjection;
using Repositories.UnitOfWork;
using Services;

namespace Services.DI
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddBookingServices(this IServiceCollection services)
        {
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IPaymentGateway>(sp =>
                new MockOnlineGateway(PaymentMethod.VnPay));
            services.AddScoped<IPaymentGateway>(sp =>
                new MockOnlineGateway(PaymentMethod.MoMo));
            services.AddScoped<IPaymentGateway>(sp =>
                new MockOnlineGateway(PaymentMethod.Zalopay));
            services.AddScoped<IPaymentGateway>(sp =>
                new OnSiteGateway(PaymentMethod.Cash));
            services.AddScoped<IPaymentGateway>(sp =>
                new OnSiteGateway(PaymentMethod.QR));
            services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();

            services.AddSingleton<IPricingService, PricingService>();

            services.AddScoped<JwtGenerator>();

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IBranchService, BranchService>();
            services.AddScoped<ICourtService, CourtService>();
            services.AddScoped<ITimeSlotService, TimeSlotService>();
            services.AddScoped<IMaintenanceService, MaintenanceService>();
            services.AddScoped<IVoucherService, VoucherService>();
            services.AddScoped<IPointService, PointService>();
            services.AddScoped<IGroupService, GroupService>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IStaffService, StaffService>();
            services.AddScoped<IReviewService, ReviewService>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IPaymentService, PaymentService>();

            return services;
        }
    }
}
