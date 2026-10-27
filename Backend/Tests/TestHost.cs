using System.Security.Claims;
using Badminton.Core.Security;
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
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Repositories.Context;
using Repositories.UnitOfWork;
using Services;

namespace Badminton.Tests
{
    public sealed class TestHost : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly bool _useMigrations;
        public readonly string DbName;
        public readonly string ConnectionString;
        public int Alice
        {
            get; private set;
        }
        public int Bob
        {
            get; private set;
        }
        public int Carol
        {
            get; private set;
        }
        public int Manager
        {
            get; private set;
        }
        public int ManagerOtherBranch
        {
            get; private set;
        }
        public int BranchId
        {
            get; private set;
        }
        public int BranchId2
        {
            get; private set;
        }
        public int Court1
        {
            get; private set;
        }
        public int Court2
        {
            get; private set;
        }
        public int SlotMorning
        {
            get; private set;
        }
        public int SlotNoon
        {
            get; private set;
        }
        public int SlotPeak
        {
            get; private set;
        }
        public int SlotSoon40
        {
            get; private set;
        }
        public DateTime HanoiNowUtc => DateTime.SpecifyKind(
            new DateTime(FutureDate.Year, FutureDate.Month, FutureDate.Day, 2, 0, 0),
            DateTimeKind.Utc); // 02:00 UTC == 09:00 Hanoi
        public DateOnly FutureDate => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5);

        public TestHost(string? dbName = null, Action<IServiceCollection>? configure = null, bool useMigrations = false)
        {
            _useMigrations = useMigrations;
            DbName = dbName ?? "BadmintonTest_" + Guid.NewGuid().ToString("N")[..8];
            if (!DbName.StartsWith("BadmintonTest_", StringComparison.Ordinal))
                throw new InvalidOperationException("Tests require an isolated BadmintonTest_ database.");

            var server =
                Environment.GetEnvironmentVariable("BADMINTON_TEST_SQLSERVER")
                ?? ".\\SQLEXPRESS";
            if (Environment.GetEnvironmentVariable("BADMINTON_TEST_POSTGRES") is null
                && server is not ("." or ".\\SQLEXPRESS" or "localhost" or "127.0.0.1" or "(localdb)\\MSSQLLocalDB"))
                throw new InvalidOperationException("SQL Server tests require an isolated loopback server.");
            ConnectionString =
                $"Server={server};Database={DbName};Trusted_Connection=True;TrustServerCertificate=True;";
            var postgres = Environment.GetEnvironmentVariable("BADMINTON_TEST_POSTGRES");
            if (!string.IsNullOrEmpty(postgres))
            {
                var pg = new Npgsql.NpgsqlConnectionStringBuilder(postgres) { Database = DbName };
                if (pg.Host is not ("127.0.0.1" or "localhost"))
                    throw new InvalidOperationException("PostgreSQL tests require an isolated loopback server.");
                ConnectionString = pg.ConnectionString;
            }

            // Deterministic Hanoi wall clock for the whole suite.
            HanoiTime.NowUtc = () => HanoiNowUtc;

            var services = new ServiceCollection();
            Services.DI.BookingExperienceRegistration.AddBookingExperience(services);
            if (postgres is not null)
            {
                services.AddDbContext<PostgresBookingDbContext>((sp, o) => o.UseNpgsql(ConnectionString).AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()));
                services.AddScoped<BadmintonBookingDbContext>(sp => sp.GetRequiredService<PostgresBookingDbContext>());
            }
            else
                services.AddDbContext<BadmintonBookingDbContext>((sp, o) => o.UseSqlServer(ConnectionString).AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<IOperationsReadStore, OperationsReadStore>();
            services.AddSingleton<IPricingService, PricingService>();

            // A mutable HttpContextAccessor the tests can pin a principal to.
            services.AddSingleton<TestHttpContextAccessor>();
            services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestHttpContextAccessor>());

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
            services.AddScoped<Contract.Services.Account.IAccountService, AccountService>();
            services.AddScoped<BookingAvailabilityService>();
            services.AddScoped<BookingCreationService>();
            services.AddScoped<BookingQuoteService>();
            services.AddScoped<BookingLifecycleService>();
            services.AddScoped<BookingCompletionService>();
            services.AddScoped<BookingExpiryService>();
            services.AddScoped<IBookingService, BookingService>();
            services.AddScoped<IPaymentService, PaymentService>();

            // ── FEAT defaults: in-memory / no external HTTP in tests ───
            services.AddLogging();
            services.AddSingleton<Microsoft.Extensions.Options.IOptions<Badminton.Core.Store.NotifySettings>>(_ =>
                Microsoft.Extensions.Options.Options.Create(
                    new Badminton.Core.Store.NotifySettings { Channels = "log" }));
            services.AddSingleton<Microsoft.Extensions.Options.IOptions<Badminton.Core.Store.MembershipSettings>>(_ =>
                Microsoft.Extensions.Options.Options.Create(
                    new Badminton.Core.Store.MembershipSettings
                    {
                        MultiplierSilver = 1.5m,
                        MultiplierGold = 2.0m,
                        SilverDays = 30,
                        GoldDays = 90
                    }));
            services.AddSingleton<Contract.Services.Notify.INotifyChannel>(sp =>
                new LogNotifyChannel(
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LogNotifyChannel>>()));
            services.AddScoped<Contract.Services.Notify.INotifyService, NotifyService>();
            services.AddScoped<Contract.Services.Membership.IMembershipService, MembershipService>();
            services.AddScoped<Contract.Services.Booking.ICheckinService, CheckinService>();
            services.AddScoped<Contract.Services.Maintenance.IMaintenanceService, MaintenanceService>();
            services.AddScoped<Contract.Services.Waitlist.IWaitlistService, WaitlistService>();

            configure?.Invoke(services);
            _services = services.BuildServiceProvider();
            EnsureCreatedAndSeed();
        }

        private void EnsureCreatedAndSeed()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BadmintonBookingDbContext>();
            // CI LocalDB can spend longer creating database files/schema than a
            // business query. Bound setup separately; keep normal query timeouts.
            var queryTimeout = db.Database.GetCommandTimeout();
            try
            {
                db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));
                db.Database.EnsureDeleted();
                if (_useMigrations || db.Database.IsNpgsql())
                    db.Database.Migrate();
                else
                    db.Database.EnsureCreated();
            }
            finally
            {
                db.Database.SetCommandTimeout(queryTimeout);
            }

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

            // A second branch to prove branch-scoping (Q3=A).
            var branch2 = new Branch
            {
                Name = "Branch T2",
                Address = "Test 2",
                OpenTime = new TimeOnly(6, 0),
                CloseTime = new TimeOnly(23, 0),
                Status = BranchStatus.Active,
                IsActive = true,
                CreatedAt = now
            };
            db.Branches.Add(branch2);
            db.SaveChanges();
            BranchId2 = branch2.Id;

            var s1 = new TimeSlot { Label = "10:00", StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(11, 0), IsPeak = false, IsActive = true, CreatedAt = now };
            var s2 = new TimeSlot { Label = "12:00", StartTime = new TimeOnly(12, 0), EndTime = new TimeOnly(13, 0), IsPeak = false, IsActive = true, CreatedAt = now };
            var s3 = new TimeSlot { Label = "18:00", StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(19, 0), IsPeak = true, IsActive = true, CreatedAt = now };
            // 09:40 slot: starts 40 min after the pinned 09:00 "now" → bookable.
            var s4 = new TimeSlot { Label = "09:40", StartTime = new TimeOnly(9, 40), EndTime = new TimeOnly(10, 40), IsPeak = false, IsActive = true, CreatedAt = now };
            db.TimeSlots.AddRange(s1, s2, s3, s4);
            db.SaveChanges();
            SlotMorning = s1.Id;
            SlotNoon = s2.Id;
            SlotPeak = s3.Id;
            SlotSoon40 = s4.Id;

            var c1 = new Court { BranchId = BranchId, Code = "S01", Name = "Court 1", PriceLowPeak = 100_000, PricePeak = 150_000, WeekendSurcharge = 0.20m, Capacity = 2, IsActive = true, CreatedAt = now };
            var c2 = new Court { BranchId = BranchId, Code = "S02", Name = "Court 2", PriceLowPeak = 100_000, PricePeak = 150_000, WeekendSurcharge = 0.20m, Capacity = 2, IsActive = true, CreatedAt = now };
            var c3 = new Court { BranchId = BranchId2, Code = "S01", Name = "Court 1B", PriceLowPeak = 100_000, PricePeak = 150_000, WeekendSurcharge = 0.20m, Capacity = 2, IsActive = true, CreatedAt = now };
            db.Courts.AddRange(c1, c2, c3);
            db.SaveChanges();
            Court1 = c1.Id;
            Court2 = c2.Id;

            var alice = MakeUser("Alice", "0900000001", Role.Customer);
            var bob = MakeUser("Bob", "0900000002", Role.Customer);
            var carol = MakeUser("Carol", "0900000003", Role.Customer);
            var manager = MakeUser("Manager", "0900000004", Role.Manager);
            manager.BranchId = BranchId;
            var manager2 = MakeUser("Manager2", "0900000005", Role.Manager);
            manager2.BranchId = BranchId2;
            var admin = MakeUser("Admin", "0900000006", Role.Admin);
            db.Users.AddRange(alice, bob, carol, manager, manager2, admin);
            db.SaveChanges();
            Alice = alice.Id;
            Bob = bob.Id;
            Carol = carol.Id;
            Manager = manager.Id;
            ManagerOtherBranch = manager2.Id;

            ActAs(Manager, Role.Manager);
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

        public async Task RunScopeAsync(Func<IServiceProvider, Task> fn)
        {
            using var scope = _services.CreateScope();
            await fn(scope.ServiceProvider);
        }

        public T Resolve<T>() where T : class
        {
            using var scope = _services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<T>();
        }

        public void ActAs(int userId, params Role[] roles)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
            foreach (var r in roles)
                claims.Add(new(ClaimTypes.Role, r.ToString()));
            _services.GetRequiredService<TestHttpContextAccessor>().HttpContext =
                new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                };
        }

        // ── Booking helpers (shared by lifecycle/refund/redeem tests) ────
        public async Task<ModelViews.Booking.BookingView> BookAsync(int user, int court, int slot,
            string date, int? pointsToRedeem = null, string? voucherCode = null)
        {
            return await RunScopeAsync(sp =>
                sp.GetRequiredService<IBookingService>().CreateAsync(
                    new ModelViews.Booking.CreateBookingRequest(court, date, slot,
                        1, null, voucherCode, Guid.NewGuid().ToString("N"), null, pointsToRedeem),
                    user));
        }

        public async Task<ModelViews.Payment.PaymentView> ConfirmOnSiteAsync(
            int bookingId, int actorId, string method = "Cash")
        {
            ActAs(actorId, Role.Manager);
            return await RunScopeAsync(sp =>
                sp.GetRequiredService<IPaymentService>()
                 .ConfirmOnSiteAsync(bookingId, method, actorId));
        }

        public async Task<ModelViews.Booking.BookingView> MakeConfirmedBookingAsync(
            int user, int court, int slot, int? pointsToRedeem = null)
        {
            var view = await BookAsync(user, court, slot, FutureDate.ToString("yyyy-MM-dd"), pointsToRedeem);
            await ConfirmOnSiteAsync(view!.Id, Manager);
            return view!;
        }

        // Exercise completion through a real check-in and the end-of-slot business clock.
        public async Task<ModelViews.Booking.BookingView> CompleteAsPlayedAsync(int id, int actorId, string? note = null)
        {
            var original = HanoiTime.NowUtc;
            try
            {
                var booking = await GetBookingAsync(id);
                var slot = await RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().TimeSlots.SingleAsync(s => s.Id == booking.SlotId));
                HanoiTime.NowUtc = () => DateTime.SpecifyKind(HanoiTime.SlotStart(booking.Date, slot.StartTime).Add(-HanoiTime.FixedOffset), DateTimeKind.Utc);
                await RunScopeAsync(sp => sp.GetRequiredService<ICheckinService>().CheckinByCodeAsync(booking.BookingCode, actorId));
                HanoiTime.NowUtc = () => DateTime.SpecifyKind(HanoiTime.SlotEnd(booking.Date, slot.EndTime).Add(-HanoiTime.FixedOffset).AddSeconds(1), DateTimeKind.Utc);
                return await RunScopeAsync(sp => sp.GetRequiredService<IBookingService>().MarkCompletedAsync(id, actorId, note));
            }
            finally { HanoiTime.NowUtc = original; }
        }

        public int BranchManager => Manager;

        public async Task<Voucher> AddVoucherAsync(string code, int totalQty, int maxPerUser,
            int value, VoucherType type = VoucherType.Fixed, int minSpend = 0)
        {
            var v = await RunScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();

                var actualToday = DateOnly.FromDateTime(DateTime.UtcNow);
                var row = new Voucher
                {
                    Code = code,
                    Type = type,
                    Value = value,
                    MinSpend = minSpend,
                    MaxPerUser = maxPerUser,
                    TotalQty = totalQty,
                    UsedQty = 0,
                    ValidFrom = actualToday.AddDays(-1),
                    ValidUntil = actualToday.AddDays(30),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.Vouchers.Add(row);
                await db.SaveChangesAsync();
                return row;
            })!;
            return v;
        }

        public async Task CountPointsIntoAsync(int userId, int amount, string reason)
        {
            await RunScopeAsync(async sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                db.PointLedger.Add(new PointLedger
                {
                    UserId = userId,
                    Delta = amount,
                    Reason = reason,
                    LedgerKey = null
                });
                await db.SaveChangesAsync();
            });
        }

        public async Task<int> CountPointRowsAsync(int bookingId)
        {
            return await RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                return db.PointLedger.CountAsync(p => p.BookingId == bookingId);
            });
        }

        public async Task<int> CountRefundRowsAsync(int bookingId)
        {
            return await RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                return db.Payments.CountAsync(p => p.BookingId == bookingId
                    && (p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartialRefund));
            });
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

        public async Task<User> GetUserAsync(int userId)
        {
            var u = await RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                return db.Users.FirstAsync(x => x.Id == userId);
            });
            return u!;
        }

        public async Task<Booking> GetBookingAsync(int bookingId)
        {
            var b = await RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                return db.Bookings.FirstAsync(x => x.Id == bookingId);
            });
            return b!;
        }

        public async Task<int> CountBookingsForSlotAsync(int courtId, DateOnly date, int slotId)
        {
            return await RunScopeAsync(sp =>
            {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                return db.Bookings.CountAsync(b =>
                    b.CourtId == courtId && b.Date == date && b.SlotId == slotId
                    && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            });
        }

        public ValueTask DisposeAsync()
        {
            HanoiTime.NowUtc = () => DateTime.UtcNow;
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BadmintonBookingDbContext>();
            db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));
            db.Database.EnsureDeleted();
            _services.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    public sealed class TestHttpContextAccessor : IHttpContextAccessor, ICurrentUser
    {
        public HttpContext? HttpContext
        {
            get; set;
        }
        public ClaimsPrincipal Principal => HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    }
}
