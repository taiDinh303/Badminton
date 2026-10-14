using System.Security.Claims;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Booking;
using Contract.Services.Group;
using Contract.Services.Payment;
using Contract.Services.Points;
using Contract.Services.Pricing;
using Contract.Services.Voucher;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using M.Core.Domain;
using M.Core.Enums;
using Repositories.Context;
using Repositories.UnitOfWork;
using Services;

namespace Badminton.Tests
{
    public sealed class TestHost : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        public readonly string DbName;
        public readonly string ConnectionString;

        /// <summary>Captured user ids after seeding (EF-generated).</summary>
        public int Alice { get; private set; }
        public int Bob { get; private set; }
        public int Carol { get; private set; }
        public int Manager { get; private set; }

        public int BranchId { get; private set; }
        public int Court1 { get; private set; }
        public int Court2 { get; private set; }
        public int SlotMorning { get; private set; }
        public int SlotNoon { get; private set; }
        public int SlotPeak { get; private set; }

        /// <summary>A booking date 5 days out (always &gt;30 min, &lt;30 days).</summary>
        public DateOnly FutureDate => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);

        public TestHost(string? dbName = null)
        {
            DbName = dbName ?? "BadmintonTest_" + Guid.NewGuid().ToString("N")[..8];
            ConnectionString =
                $"Server=(localdb)\\MSSQLLocalDB;Database={DbName};Trusted_Connection=True;TrustServerCertificate=True;";

            var services = new ServiceCollection();
            services.AddDbContext<BadmintonBookingDbContext>(o => o.UseSqlServer(ConnectionString));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton<IPricingService, PricingService>();

            // A mutable HttpContextAccessor the tests can pin a principal to.
            services.AddSingleton<TestHttpContextAccessor>();
            services.AddScoped<IHttpContextAccessor>(sp => sp.GetRequiredService<TestHttpContextAccessor>());

            services.AddScoped<IPaymentGateway>(sp => new MockOnlineGateway(PaymentMethod.VnPay));
            services.AddScoped<IPaymentGateway>(sp => new MockOnlineGateway(PaymentMethod.MoMo));
            services.AddScoped<IPaymentGateway>(sp => new MockOnlineGateway(PaymentMethod.Zalopay));
            services.AddScoped<IPaymentGateway>(sp => new OnSiteGateway(PaymentMethod.Cash));
            services.AddScoped<IPaymentGateway>(sp => new OnSiteGateway(PaymentMethod.QR));
            services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();

            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IGroupService, GroupService>();
            services.AddScoped<IPointService, PointService>();
            services.AddScoped<IVoucherService, VoucherService>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IPaymentService, PaymentService>();

            _services = services.BuildServiceProvider();
            EnsureCreatedAndSeed();
        }

        private void EnsureCreatedAndSeed()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BadmintonBookingDbContext>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            var now = DateTime.UtcNow;

            var branch = new Branch
            {
                Name = "Branch T",
                Address = "Test",
                OpenTime = new TimeOnly(6, 0),
                CloseTime = new TimeOnly(23, 0),
                Status = BranchStatus.Active,
                IsActive = true,
                CreatedAt = now
            };
            db.Branches.Add(branch);
            db.SaveChanges();
            BranchId = branch.Id;

            var s1 = new TimeSlot { Label = "10:00", StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0), IsPeak = false, IsActive = true, CreatedAt = now };
            var s2 = new TimeSlot { Label = "12:00", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(13, 0), IsPeak = false, IsActive = true, CreatedAt = now };
            var s3 = new TimeSlot { Label = "18:00", StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(19, 0), IsPeak = true, IsActive = true, CreatedAt = now };
            db.TimeSlots.AddRange(s1, s2, s3);
            db.SaveChanges();
            SlotMorning = s1.Id; SlotNoon = s2.Id; SlotPeak = s3.Id;

            var c1 = new Court { BranchId = BranchId, Code = "S01", Name = "Court 1", PriceLowPeak = 100_000, PricePeak = 150_000, WeekendSurcharge = 0.20m, Capacity = 2, IsActive = true, CreatedAt = now };
            var c2 = new Court { BranchId = BranchId, Code = "S02", Name = "Court 2", PriceLowPeak = 100_000, PricePeak = 150_000, WeekendSurcharge = 0.20m, Capacity = 2, IsActive = true, CreatedAt = now };
            db.Courts.AddRange(c1, c2);
            db.SaveChanges();
            Court1 = c1.Id; Court2 = c2.Id;

            var alice = MakeUser("Alice", "0900000001", Role.Customer);
            var bob = MakeUser("Bob", "0900000002", Role.Customer);
            var carol = MakeUser("Carol", "0900000003", Role.Customer);
            var manager = MakeUser("Manager", "0900000004", Role.Manager);
            db.Users.AddRange(alice, bob, carol, manager);
            db.SaveChanges();
            Alice = alice.Id; Bob = bob.Id; Carol = carol.Id; Manager = manager.Id;
        }

        private static User MakeUser(string name, string phone, Role role) => new()
        {
            FullName = name,
            PhoneNumber = phone,
            PasswordHash = "x",
            Role = role,
            Status = UserStatus.Active,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        public async Task<T> RunScopeAsync<T>(Func<IServiceProvider, Task<T>> fn)
        {
            using var scope = _services.CreateScope();
            return await fn(scope.ServiceProvider);
        }

        /// <summary>Force the ambient HttpContext to represent a user with roles.</summary>
        public void ActAs(int userId, params Role[] roles)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
            foreach (var r in roles) claims.Add(new(ClaimTypes.Role, r.ToString()));
            _services.GetRequiredService<TestHttpContextAccessor>().HttpContext =
                new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                };
        }

        public async Task<Voucher> AddVoucherAsync(string code, int totalQty, int maxPerUser,
            int value, VoucherType type = VoucherType.Fixed, int minSpend = 0)
        {
            var v = await RunScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                var row = new Voucher
                {
                    Code = code,
                    Type = type,
                    Value = value,
                    MinSpend = minSpend,
                    MaxPerUser = maxPerUser,
                    TotalQty = totalQty,
                    UsedQty = 0,
                    ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                    ValidUntil = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.Vouchers.Add(row);
                await db.SaveChangesAsync();
                return row;
            })!;
            return v;
        }

        public async Task<int> CountBookingsForSlotAsync(int courtId, DateOnly date, int slotId)
        {
            var n = await RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                return db.Bookings.CountAsync(b =>
                    b.CourtId == courtId && b.Date == date && b.SlotId == slotId
                    && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            });
            return n;
        }

        public async Task<int> CountPointRowsAsync(int bookingId)
        {
            var n = await RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                return db.PointLedger.CountAsync(p => p.BookingId == bookingId);
            });
            return n;
        }

        public async Task<int> CountRefundRowsAsync(int bookingId)
        {
            var n = await RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                return db.Payments.CountAsync(p => p.BookingId == bookingId
                    && (p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartialRefund));
            });
            return n;
        }

        public async Task<Voucher> GetVoucherAsync(string code)
        {
            var v = await RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                return db.Vouchers.FirstAsync(x => x.Code == code);
            });
            return v!;
        }

        public ValueTask DisposeAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BadmintonBookingDbContext>();
            db.Database.EnsureDeleted();
            _services.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    public sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }
}
