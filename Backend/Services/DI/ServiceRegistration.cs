using Contract.Services.Account;
using Contract.Services.Audit;
using Contract.Services.Auth;
using Contract.Services.Booking;
using Contract.Services.Branch;
using Contract.Services.Court;
using Contract.Services.Group;
using Contract.Services.Maintenance;
using Contract.Services.Membership;
using Contract.Services.Notify;
using Contract.Services.Payment;
using Contract.Services.Points;
using Contract.Services.Pricing;
using Contract.Services.Review;
using Contract.Services.Report;
using Contract.Services.Staff;
using Contract.Services.TimeSlot;
using Contract.Services.Voucher;
using Contract.Services.Waitlist;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Store;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services;

namespace Services.DI
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddBookingServices(
            this IServiceCollection services, IConfiguration? configuration = null)
        {

            services.AddScoped<Contract.Services.Contact.IContactService, ContactService>();

            // On-site gateways never move real money — always registered.
            services.AddScoped<IPaymentGateway>(sp =>
                new OnSiteGateway(PaymentMethod.Cash));
            services.AddScoped<IPaymentGateway>(sp =>
                new OnSiteGateway(PaymentMethod.QR));

            var provider = (configuration?["Payment:Provider"] ?? "mock").Trim();

            if (string.Equals(provider, "real", System.StringComparison.OrdinalIgnoreCase))
            {
                // Real MoMo adapter (inert until a secret is configured).
                IConfigurationSection momoSection = configuration is not null
                    ? configuration.GetSection("Payment:Momo")
                    : new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build().GetSection("Payment:Momo");
                services.Configure<MoMoSettings>(momoSection);
                services.AddHttpClient("momo",
                    c => c.BaseAddress = new Uri(
                        configuration?["Payment:Momo:Endpoint"]
                        ?? "https://app.tapmomo.vn"));
                services.AddSingleton<IMomoApiClient>(sp =>
                    new HttpMoMoApiClient(
                        sp.GetRequiredService<IHttpClientFactory>()
                           .CreateClient("momo"),
                        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MoMoSettings>>().Value,
                        MomoPayMethod.momo_wallet));
                services.AddScoped<IPaymentGateway>(sp =>
                    new MoMoGateway(
                        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MoMoSettings>>().Value,
                        sp.GetRequiredService<IMomoApiClient>()));

                // ── Real VNPAY adapter (inert until MerchantCode+SecretKey). ──
                IConfigurationSection vnSection = configuration is not null
                    ? configuration.GetSection("Payment:Vnpay")
                    : new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build().GetSection("Payment:Vnpay");
                services.Configure<VnPaySettings>(vnSection);
                services.AddHttpClient("vnpay",
                    c => c.BaseAddress = new Uri(
                        configuration?["Payment:Vnpay:Endpoint"]
                        ?? "https://sandbox.vnpay.io"));
                services.AddSingleton<IVnPayClient>(sp =>
                    new HttpVnPayClient(
                        sp.GetRequiredService<IHttpClientFactory>().CreateClient("vnpay"),
                        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VnPaySettings>>().Value));
                services.AddScoped<IPaymentGateway>(sp =>
                    new VnPayGateway(
                        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VnPaySettings>>().Value,
                        sp.GetRequiredService<IVnPayClient>()));

                // ── Real Zalopay adapter (inert until ApiKey+ApiSecret). ──
                IConfigurationSection zpSection = configuration is not null
                    ? configuration.GetSection("Payment:Zalopay")
                    : new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build().GetSection("Payment:Zalopay");
                services.Configure<ZalopaySettings>(zpSection);
                services.AddHttpClient("zalopay",
                    c => c.BaseAddress = new Uri(
                        configuration?["Payment:Zalopay:Endpoint"]
                        ?? "https://external.zalopay.net"));
                services.AddSingleton<IZalopayClient>(sp =>
                    new HttpZalopayClient(
                        sp.GetRequiredService<IHttpClientFactory>().CreateClient("zalopay"),
                        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ZalopaySettings>>().Value));
                services.AddScoped<IPaymentGateway>(sp =>
                    new ZalopayGateway(
                        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ZalopaySettings>>().Value,
                        sp.GetRequiredService<IZalopayClient>()));
            }
            else if (string.Equals(provider, "mock", System.StringComparison.OrdinalIgnoreCase))
            {
                // P0-3: mock online gateways only in mock/dev mode.
                services.AddScoped<IPaymentGateway>(sp =>
                    new MockOnlineGateway(PaymentMethod.VnPay));
                services.AddScoped<IPaymentGateway>(sp =>
                    new MockOnlineGateway(PaymentMethod.MoMo));
                services.AddScoped<IPaymentGateway>(sp =>
                    new MockOnlineGateway(PaymentMethod.Zalopay));
            }

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
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IPaymentService, PaymentService>();

            // FEAT 3 — QR check-in.
            services.AddScoped<ICheckinService, CheckinService>();

            // FEAT 4 — Membership V2 (tier multiplier, config-driven).
            IConfigurationSection membershipSection = configuration is not null
                ? configuration.GetSection("Membership")
                : new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()
                    .GetSection("Membership");
            services.Configure<MembershipSettings>(membershipSection);
            services.AddScoped<IMembershipService, MembershipService>();

            IConfigurationSection notifySection = configuration is not null
                ? configuration.GetSection("Notify")
                : new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()
                    .GetSection("Notify");
            services.Configure<NotifySettings>(notifySection);
            services.AddHttpClient("notify-email");
            services.AddHttpClient("notify-sms");
            services.AddHttpClient("notify-zalo");
            services.AddSingleton<INotifyChannel>(sp =>
                new LogNotifyChannel(
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LogNotifyChannel>>()));
            services.AddSingleton<INotifyChannel>(sp =>
                new EmailNotifyChannel(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotifySettings>>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EmailNotifyChannel>>()));
            services.AddSingleton<INotifyChannel>(sp =>
                new SmsNotifyChannel(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotifySettings>>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SmsNotifyChannel>>()));
            services.AddSingleton<INotifyChannel>(sp =>
                new ZaloNotifyChannel(
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotifySettings>>(),
                    sp.GetRequiredService<IHttpClientFactory>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ZaloNotifyChannel>>()));
            services.AddScoped<INotifyService, NotifyService>();

            // FEAT 2 — Waitlist (join/leave/promote + promotion job).
            services.AddScoped<IWaitlistService, WaitlistService>();
            services.AddScoped<IAccountService, AccountService>();

            return services;
        }
    }
}
