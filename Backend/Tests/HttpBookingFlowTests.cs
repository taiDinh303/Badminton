using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Badminton.API.Configuration;
using Badminton.API.Middleware;
using Badminton.Core.Store;
using Badminton.Core.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Repositories.Context;
using Services;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Badminton.Tests;

public sealed class HttpBookingFlowTests : IAsyncLifetime
{
    private readonly TestHost _fixture = new(useMigrations: true);
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly JwtSettings _jwt = new()
    {
        Key = new string('t', 64), Issuer = "BadmintonHttpTests", Audience = "BadmintonHttpTests", ExpireMinutes = 10
    };

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(Badminton.API.Program).Assembly.GetName().Name,
            EnvironmentName = "Testing",
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:BadmintonBooking"] = _fixture.ConnectionString,
            ["Database:Provider"] = Environment.GetEnvironmentVariable("BADMINTON_TEST_POSTGRES") is null ? "SqlServer" : "PostgreSQL",
            ["JwtSettings:Key"] = _jwt.Key,
            ["JwtSettings:Issuer"] = _jwt.Issuer,
            ["JwtSettings:Audience"] = _jwt.Audience,
            ["JwtSettings:ExpireMinutes"] = "10",
            ["BackgroundJobs:Enabled"] = "false",
            ["Payment:Provider"] = "mock",
            ["Notify:Channels"] = "log",
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        Assert.Empty(StartupConfiguration.Errors(builder.Configuration, builder.Environment));
        builder.Services.AddBookingApi(builder.Configuration, builder.Environment);
        builder.Services.RemoveAll<IPaymentGateway>();
        var transport = new CallbackOnlyTransport();
        builder.Services.AddSingleton<IPaymentGateway>(new OnSiteGateway(PaymentMethod.Cash));
        builder.Services.AddSingleton<IPaymentGateway>(new OnSiteGateway(PaymentMethod.QR));
        builder.Services.AddSingleton<IPaymentGateway>(new VnPayGateway(new VnPaySettings { MerchantCode = "M00001", SecretKey = "http-callback-key" }, transport));
        builder.Services.AddSingleton<IPaymentGateway>(new MoMoGateway(new MoMoSettings { AppId = "test", AccessKey = "audit-access", SecretKey = "http-callback-key" }, transport));
        builder.Services.AddSingleton<IPaymentGateway>(new ZalopayGateway(new ZalopaySettings { AppId = "1234", ApiKey = "key1", ApiSecret = "http-callback-key", CallbackKey = "http-callback-key" }, transport));
        _app = builder.Build();
        _app.UseRouting();
        _app.UseMiddleware<RequestIdMiddleware>();
        _app.UseMiddleware<ExceptionMiddleware>();
        _app.UseAuthentication();
        _app.UseRateLimiter();
        _app.UseAuthorization();
        _app.MapControllers();
        _app.MapBookingHealth();
        await _app.StartAsync();
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _client = new HttpClient { BaseAddress = new Uri(address) };
    }

    private void ActAs(int id, string role)
    {
        var token = new JwtGenerator(Options.Create(_jwt)).Generate(id, "HTTP test", "0900000000", role).access;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<int> CreateBooking()
    {
        ActAs(_fixture.Alice, "Customer");
        var response = await _client.PostAsJsonAsync("/api/v1/bookings", new
        {
            courtId = _fixture.Court1, date = _fixture.FutureDate.ToString("yyyy-MM-dd"), slotId = _fixture.SlotNoon,
            playerCount = 1, idempotencyKey = Guid.NewGuid().ToString("N")
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pending", json.GetProperty("data").GetProperty("status").GetString());
        return json.GetProperty("data").GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Locked_deleted_unverified_and_changed_role_tokens_are_rejected()
    {
        ActAs(_fixture.Alice, "Customer");
        foreach (var state in new[] { "locked", "deleted", "unverified", "role" })
        {
            await _fixture.RunScopeAsync(async sp => {
                var db = sp.GetRequiredService<BadmintonBookingDbContext>();
                var user = await db.Users.FindAsync(_fixture.Alice);
                user!.Status = state == "locked" ? Badminton.Core.Enums.UserStatus.Locked : Badminton.Core.Enums.UserStatus.Active;
                user.DeletedAt = state == "deleted" ? DateTime.UtcNow : null;
                user.IsVerified = state != "unverified";
                user.Role = state == "role" ? Badminton.Core.Enums.Role.Manager : Badminton.Core.Enums.Role.Customer;
                await db.SaveChangesAsync();
            });
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/groups", new { name = "Blocked", maxMembers = 2 })).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/account/summary")).StatusCode);
        }
    }

    [Fact]
    public async Task Group_member_PII_requires_membership_even_with_a_valid_JWT()
    {
        ActAs(_fixture.Alice, "Customer");
        var created = await _client.PostAsJsonAsync("/api/v1/groups", new { name = "Private", maxMembers = 2 });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var groupData = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        var id = groupData.GetProperty("id").GetInt32();
        var inviteCode = groupData.GetProperty("inviteCode").GetString();
        ActAs(_fixture.Carol, "Customer");
        var denied = await _client.GetAsync($"/api/v1/groups/{id}/members");
        Assert.Equal(HttpStatusCode.Conflict, denied.StatusCode);
        Assert.DoesNotContain("0900000001", await denied.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/v1/groups/{id}/join", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync($"/api/v1/groups/{id}/join", new { inviteCode })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/v1/groups/{id}/members")).StatusCode);
    }

    [Fact]
    public async Task Voucher_HTTP_contract_preserves_dates_and_rejects_invalid_quotes()
    {
        var voucher = await _fixture.AddVoucherAsync("DATES", 10, 1, 10000);
        ActAs(_fixture.Alice, "Customer");
        var view = (await _client.GetFromJsonAsync<JsonElement>("/api/v1/vouchers/DATES")).GetProperty("data");
        Assert.Equal(voucher.ValidFrom.ToString("yyyy-MM-dd"), view.GetProperty("validFrom").GetString());
        Assert.Equal(voucher.ValidUntil!.Value.ToString("yyyy-MM-dd"), view.GetProperty("validUntil").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/bookings/quote", new {
            courtId = _fixture.Court1, date = _fixture.FutureDate.ToString("yyyy-MM-dd"), slotId = _fixture.SlotNoon, voucherCode = "UNKNOWN"
        })).StatusCode);
    }

    [Fact]
    public async Task Malformed_native_callbacks_are_rejected_without_changing_orders()
    {
        Assert.Equal("97", (await _client.GetFromJsonAsync<JsonElement>("/api/v1/payments/callback/vnpay?vnp_TxnRef=UNKNOWN")).GetProperty("RspCode").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/payments/callback/momo", new { orderId = "UNKNOWN" })).StatusCode);
        var rejected = await _client.PostAsJsonAsync("/api/v1/payments/callback/zalopay", new { data = "not-json" });
        Assert.Equal(2, (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("return_code").GetInt32());
    }

    [Theory]
    [InlineData("VnPay")]
    [InlineData("MoMo")]
    [InlineData("Zalopay")]
    public async Task Native_signed_callback_confirms_once_and_acknowledges_replay(string provider)
    {
        var id = await CreateBooking();
        var amount = (await _fixture.GetBookingAsync(id)).PayableAmount;
        await _fixture.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            db.Payments.Add(new Contract.Repositories.Entity.Payment { BookingId = id, UserId = _fixture.Alice, Amount = amount,
                Method = Enum.Parse<PaymentMethod>(provider), Status = PaymentStatus.Pending, GatewayRef = "HTTP-ORDER" });
            await db.SaveChangesAsync();
        });
        var signed = provider switch {
            "VnPay" => PaymentCallbackFixtures.VnPay("http-callback-key", "HTTP-ORDER", amount),
            "MoMo" => PaymentCallbackFixtures.MoMo("http-callback-key", "HTTP-ORDER", amount: amount),
            _ => PaymentCallbackFixtures.Zalo("http-callback-key", "HTTP-ORDER", amount)
        };
        _client.DefaultRequestHeaders.Authorization = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = provider == "VnPay"
                ? await _client.GetAsync("/api/v1/payments/callback/vnpay?" + signed.Body.TrimStart('?'))
                : await _client.PostAsync("/api/v1/payments/callback/" + provider.ToLowerInvariant(), new StringContent(signed.Body, System.Text.Encoding.UTF8, "application/json"));
            if (provider == "MoMo") Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
            else {
                Assert.Equal(HttpStatusCode.OK, result.StatusCode);
                var json = await result.Content.ReadFromJsonAsync<JsonElement>();
                if (provider == "VnPay") Assert.Equal("00", json.GetProperty("RspCode").GetString());
                else Assert.Equal(1, json.GetProperty("return_code").GetInt32());
            }
        }
        Assert.Equal(BookingStatus.Confirmed, (await _fixture.GetBookingAsync(id)).Status);
        Assert.Equal(1, await _fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.CountAsync(p => p.BookingId == id && p.Status == PaymentStatus.Success)));
    }

    private sealed class CallbackOnlyTransport : IVnPayClient, IMomoApiClient, IZalopayClient
    {
        public Task<VnPayCreateResult> CreatePayUrlAsync(IReadOnlyDictionary<string, string> fields, CancellationToken ct) => throw new InvalidOperationException("Callback tests must not call network.");
        public Task<MomoPayInit> CreatePayUrlAsync(MomoPayRequest request, CancellationToken ct) => throw new InvalidOperationException("Callback tests must not call network.");
        public Task<ZpOrderCreate> CreateOrderAsync(ZpCreateOrderRequest request, CancellationToken ct) => throw new InvalidOperationException("Callback tests must not call network.");
    }

    private async Task SetSyntheticPassword()
    {
        await _fixture.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var user = await db.Users.FindAsync(_fixture.Alice);
            user!.PasswordHash = BCrypt.Net.BCrypt.HashPassword("SyntheticOld$2026"); await db.SaveChangesAsync();
        });
    }
    private async Task LoginAlice(string password)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new { phoneNumber = "0900000001", password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("accessToken").GetString();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task Password_change_revokes_old_JWT_and_requires_new_password()
    {
        await SetSyntheticPassword(); await LoginAlice("SyntheticOld$2026");
        var oldToken = _client.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Incorrect$2026", newPassword = "SyntheticNew$2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/account/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "SyntheticOld$2026", newPassword = "SyntheticNew$2026" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/account/profile")).StatusCode);
        _client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/auth/login", new { phoneNumber = "0900000001", password = "SyntheticOld$2026" })).StatusCode);
        await LoginAlice("SyntheticNew$2026");
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/account/profile")).StatusCode);
        _client.DefaultRequestHeaders.Authorization = oldToken;
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/account/profile")).StatusCode);
    }

    [Fact]
    public async Task Server_logout_revokes_all_preexisting_tokens_and_allows_fresh_login()
    {
        await SetSyntheticPassword(); await LoginAlice("SyntheticOld$2026");
        var oldToken = _client.DefaultRequestHeaders.Authorization;
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync("/api/v1/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/account/profile")).StatusCode);
        await LoginAlice("SyntheticOld$2026");
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/account/profile")).StatusCode);
        _client.DefaultRequestHeaders.Authorization = oldToken;
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/account/profile")).StatusCode);
    }

    [Fact]
    public async Task Lock_then_unlock_does_not_reactivate_the_old_staff_JWT()
    {
        ActAs(_fixture.Manager, "Manager"); var old = _client.DefaultRequestHeaders.Authorization;
        var admin = await _fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Users.Where(u => u.Role == Role.Admin).Select(u => u.Id).SingleAsync());
        ActAs(admin, "Admin");
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/v1/admin/staff/{_fixture.Manager}/lock", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/v1/admin/staff/{_fixture.Manager}/unlock", null)).StatusCode);
        _client.DefaultRequestHeaders.Authorization = old;
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/account/profile")).StatusCode);
    }

    [Fact]
    public async Task Reconciliation_HTTP_requires_staff_and_enforces_branch_scope()
    {
        var id = await CreateBooking();
        await _fixture.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            db.Payments.Add(new Contract.Repositories.Entity.Payment { BookingId = id, UserId = _fixture.Alice, Amount = 100000,
                Method = PaymentMethod.MoMo, GatewayRef = "REVIEW", ReconciliationRequired = true, ReconciliationReason = "initialization_uncertain" });
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/payments/reconciliation")).StatusCode);
        ActAs(_fixture.ManagerOtherBranch, "Manager");
        var empty = await _client.GetFromJsonAsync<JsonElement>("/api/v1/payments/reconciliation");
        Assert.Empty(empty.GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await _client.GetAsync($"/api/v1/payments/reconciliation?branchId={_fixture.BranchId}")).StatusCode);
        ActAs(_fixture.Manager, "Manager");
        Assert.Single((await _client.GetFromJsonAsync<JsonElement>("/api/v1/payments/reconciliation")).GetProperty("data").GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Branch_admin_HTTP_validates_storage_limits_and_opening_hours()
    {
        var admin = await _fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Users
            .Where(u => u.Role == Badminton.Core.Enums.Role.Admin).Select(u => u.Id).SingleAsync());
        ActAs(admin, "Admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/branches", new {
            name = new string('x', 101), address = "Address", openTime = "06:00", closeTime = "23:00"
        })).StatusCode);
        var created = await _client.PostAsJsonAsync("/api/v1/branches", new {
            name = "HTTP branch", address = "Address", openTime = "06:00", closeTime = "23:00"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PatchAsJsonAsync($"/api/v1/branches/{id}", new {
            openTime = "22:00", closeTime = "06:00"
        })).StatusCode);
        var branch = (await (await _client.GetAsync($"/api/v1/branches/{id}")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal("06:00", branch.GetProperty("openTime").GetString());
    }

    [Fact]
    public async Task Quote_HTTP_contract_is_read_only_and_manager_cannot_mutate_global_slots()
    {
        ActAs(_fixture.Alice, "Customer");
        var quote = await _client.PostAsJsonAsync("/api/v1/bookings/quote", new {
            courtId = _fixture.Court1, date = _fixture.FutureDate.ToString("yyyy-MM-dd"), slotId = _fixture.SlotNoon, playerCount = 2
        });
        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);
        var json = (await quote.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.True(json.GetProperty("payable").GetInt32() > 0);
        Assert.Equal(0, await _fixture.CountBookingsForSlotAsync(_fixture.Court1, _fixture.FutureDate, _fixture.SlotNoon));
        ActAs(_fixture.Manager, "Manager");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/time-slots?includeInactive=true")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync("/api/v1/time-slots", new {
            label = "11:00", startTime = "11:00", endTime = "12:00", isPeak = false
        })).StatusCode);
    }

    [Fact]
    public async Task Readiness_rejects_missing_migrations_without_applying_them()
    {
        await _fixture.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            var previous = db.Database.GetMigrations().ToArray()[^2];
            await db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(previous);
        });
        var response = await _client.GetAsync("/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("pending_migrations", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("schema").GetString());
        await _fixture.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            Assert.NotEmpty(await db.Database.GetPendingMigrationsAsync());
            await db.Database.MigrateAsync();
        });
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/ready")).StatusCode);
    }

    [Fact]
    public async Task Jwt_request_creates_one_booking_in_the_isolated_database()
    {
        var id = await CreateBooking();
        Assert.True(id > 0);
        Assert.Equal(1, await _fixture.CountBookingsForSlotAsync(_fixture.Court1, _fixture.FutureDate, _fixture.SlotNoon));
        var response = await _client.GetAsync($"/api/v1/bookings/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_identity_cannot_read_customer_bookings()
    {
        var response = await _client.GetAsync("/api/v1/bookings/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customer_booking_staff_payment_customer_cancel_round_trip_is_consistent()
    {
        var id = await CreateBooking();
        ActAs(_fixture.Manager, "Manager");
        Assert.Equal(HttpStatusCode.OK,
            (await _client.PostAsync($"/api/v1/payments/{id}/on-site?method=Cash", null)).StatusCode);
        ActAs(_fixture.Alice, "Customer");
        var confirmed = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/bookings/{id}");
        Assert.Equal("Confirmed", confirmed.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/v1/bookings/{id}/cancel", null)).StatusCode);
        Assert.Equal(0, await _fixture.CountBookingsForSlotAsync(_fixture.Court1, _fixture.FutureDate, _fixture.SlotNoon));
        Assert.Equal(1, await _fixture.CountRefundRowsAsync(id));
    }

    [Fact]
    public async Task Another_customer_cannot_cancel_the_booking()
    {
        var id = await CreateBooking();
        ActAs(_fixture.Bob, "Customer");
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/v1/bookings/{id}/cancel", null)).StatusCode);
        Assert.Equal(1, await _fixture.CountBookingsForSlotAsync(_fixture.Court1, _fixture.FutureDate, _fixture.SlotNoon));
    }

    [Fact]
    public async Task Manager_cannot_list_or_complete_another_branch_booking()
    {
        var id = await CreateBooking();
        await _fixture.ConfirmOnSiteAsync(id, _fixture.Manager);
        ActAs(_fixture.ManagerOtherBranch, "Manager");
        Assert.Equal(HttpStatusCode.Conflict, (await _client.GetAsync($"/api/v1/bookings/all?branchId={_fixture.BranchId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/v1/bookings/{id}/complete", null)).StatusCode);
        var list = await _client.GetFromJsonAsync<JsonElement>("/api/v1/bookings/all");
        Assert.Empty(list.GetProperty("data").GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Health_and_readiness_work_with_workers_intentionally_disabled()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/ready")).StatusCode);
    }

    [Fact]
    public async Task Idempotency_key_cannot_be_reused_by_another_customer()
    {
        var key = Guid.NewGuid().ToString("N");
        var payload = new { courtId = _fixture.Court1, date = _fixture.FutureDate.ToString("yyyy-MM-dd"),
            slotId = _fixture.SlotNoon, playerCount = 1, idempotencyKey = key };
        ActAs(_fixture.Alice, "Customer");
        var first = await _client.PostAsJsonAsync("/api/v1/bookings", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var retry = await _client.PostAsJsonAsync("/api/v1/bookings", payload);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        ActAs(_fixture.Bob, "Customer");
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/api/v1/bookings", payload)).StatusCode);
        Assert.Equal(1, await _fixture.CountBookingsForSlotAsync(_fixture.Court1, _fixture.FutureDate, _fixture.SlotNoon));
    }

    [Fact]
    public async Task Historical_breakdown_does_not_change_when_current_court_prices_change()
    {
        var id = await CreateBooking();
        var before = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/bookings/{id}");
        await _fixture.RunScopeAsync(async services =>
        {
            var db = services.GetRequiredService<BadmintonBookingDbContext>();
            await db.Courts.Where(court => court.Id == _fixture.Court1)
                .ExecuteUpdateAsync(update => update.SetProperty(court => court.PriceLowPeak, 999999));
        });
        var after = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/bookings/{id}");
        Assert.Equal(before.GetProperty("data").GetProperty("price").GetRawText(),
            after.GetProperty("data").GetProperty("price").GetRawText());
    }

    [Fact]
    public async Task Token_checkin_returns_camelcase_id_and_enforces_staff_branch()
    {
        var id = await CreateBooking();
        ActAs(_fixture.Manager, "Manager");
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/v1/payments/{id}/on-site?method=Cash", null)).StatusCode);
        ActAs(_fixture.Alice, "Customer");
        var booking = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/bookings/{id}");
        var token = booking.GetProperty("data").GetProperty("qrToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync("/api/v1/checkin/token", new { token })).StatusCode);
        ActAs(_fixture.ManagerOtherBranch, "Manager");
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/api/v1/checkin/token", new { token })).StatusCode);
        ActAs(_fixture.Manager, "Manager");
        Badminton.Core.Utils.HanoiTime.NowUtc = () => _fixture.HanoiNowUtc.AddHours(3);
        var response = await _client.PostAsJsonAsync("/api/v1/checkin/token", new { token });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, json.GetProperty("data").GetProperty("bookingId").GetInt32());
    }

    [Fact]
    public async Task Profile_patch_uses_identity_and_validates_input()
    {
        ActAs(_fixture.Alice, "Customer");
        var result = await _client.PatchAsJsonAsync("/api/v1/account/profile", new { fullName = "New name", email = "new@example.test", avatarUrl = "https://example.test/avatar.png", userId = _fixture.Bob, role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var profile = await result.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(_fixture.Alice, profile.GetProperty("data").GetProperty("id").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PatchAsJsonAsync("/api/v1/account/profile", new { fullName = " ", email = "bad-email" })).StatusCode);
    }

    [Fact]
    public async Task Worker_lock_excludes_another_session_and_can_be_released()
    {
        using var first = new Badminton.API.Jobs.JobLock(_app.Configuration);
        using var second = new Badminton.API.Jobs.JobLock(_app.Configuration);
        Assert.True(await first.AcquireAsync("test-worker", 0));
        Assert.False(await second.AcquireAsync("test-worker", 0));
        await first.ReleaseAsync("test-worker");
        Assert.True(await second.AcquireAsync("test-worker", 0));
        await second.ReleaseAsync("test-worker");
    }

    [Fact]
    public async Task No_show_reset_preserves_current_and_future_restrictions()
    {
        var month = Badminton.Core.Utils.HanoiTime.HanoiToday().MonthStart();
        await _fixture.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            await db.Users.Where(u => u.Id == _fixture.Alice).ExecuteUpdateAsync(s => s.SetProperty(u => u.NoShowLimited, true).SetProperty(u => u.NoShowLimitedPeriod, month.AddMonths(-1)));
            await db.Users.Where(u => u.Id == _fixture.Bob).ExecuteUpdateAsync(s => s.SetProperty(u => u.NoShowLimited, true).SetProperty(u => u.NoShowLimitedPeriod, month));
            await db.Users.Where(u => u.Id == _fixture.Manager).ExecuteUpdateAsync(s => s.SetProperty(u => u.NoShowLimited, true).SetProperty(u => u.NoShowLimitedPeriod, month.AddMonths(1)));
        });
        using var worker = ActivatorUtilities.CreateInstance<Badminton.API.Jobs.NoShowResetJob>(_app.Services);
        var tick = typeof(Badminton.API.Jobs.NoShowResetJob).GetMethod("RunOnceAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await (Task)tick.Invoke(worker, null)!;
        Assert.False((await _fixture.GetUserAsync(_fixture.Alice)).NoShowLimited);
        Assert.True((await _fixture.GetUserAsync(_fixture.Bob)).NoShowLimited);
        Assert.True((await _fixture.GetUserAsync(_fixture.Manager)).NoShowLimited);
    }

    [Fact]
    public async Task Voucher_worker_expires_yesterday_but_preserves_last_valid_day()
    {
        var yesterday = await _fixture.AddVoucherAsync("YESTERDAY", 5, 1, 10000);
        var today = await _fixture.AddVoucherAsync("TODAY", 5, 1, 10000);
        await _fixture.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>();
            await db.Vouchers.Where(v => v.Id == yesterday.Id).ExecuteUpdateAsync(u => u.SetProperty(v => v.ValidUntil, _fixture.FutureDate.AddDays(-1)));
            await db.Vouchers.Where(v => v.Id == today.Id).ExecuteUpdateAsync(u => u.SetProperty(v => v.ValidUntil, _fixture.FutureDate));
        });
        using var worker = ActivatorUtilities.CreateInstance<Badminton.API.Jobs.VoucherExpiryJob>(_app.Services);
        var tick = typeof(Badminton.API.Jobs.VoucherExpiryJob).GetMethod("RunOnceAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await (Task)tick.Invoke(worker, null)!;
        var states = await _fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Vouchers.ToDictionaryAsync(v => v.Code, v => v.IsActive));
        Assert.False(states["YESTERDAY"]);
        Assert.True(states["TODAY"]);
    }

    [Fact]
    public async Task Expired_onsite_payment_returns_conflict_and_does_not_record_money()
    {
        var id = await CreateBooking();
        await _fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings
            .Where(b => b.Id == id).ExecuteUpdateAsync(s => s.SetProperty(b => b.PendingExpiresAt, DateTime.UtcNow.AddMinutes(-1))));
        ActAs(_fixture.Manager, "Manager");
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/v1/payments/{id}/on-site?method=cash", null)).StatusCode);
        Assert.Equal(0, await _fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Payments.CountAsync(p => p.BookingId == id)));
    }

    [Fact]
    public async Task Admin_court_patch_returns_bad_request_for_invalid_capacity_and_price()
    {
        var adminId = await _fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Users
            .Where(u => u.Role == Role.Admin).Select(u => u.Id).SingleAsync());
        ActAs(adminId, "Admin");
        foreach (var body in new object[] { new { capacity = 0 }, new { priceLowPeak = -1 }, new { pricePeak = 0 }, new { weekendSurcharge = -1m } })
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PatchAsJsonAsync($"/api/v1/courts/{_fixture.Court1}", body)).StatusCode);
    }

    [Fact]
    public async Task Http_presence_deadline_and_completion_contracts_are_enforced()
    {
        var id = await CreateBooking();
        var initial = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/bookings/{id}");
        Assert.Equal(JsonValueKind.String, initial.GetProperty("data").GetProperty("pendingExpiresAt").ValueKind);
        Assert.Equal(JsonValueKind.String, initial.GetProperty("data").GetProperty("serverTimeUtc").ValueKind);
        await _fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.Where(b => b.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BookingStatus.Confirmed)));
        ActAs(_fixture.Manager, "Manager");
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/v1/bookings/{id}/complete", null)).StatusCode);
        var code = (await _fixture.GetBookingAsync(id)).BookingCode;
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/v1/checkin/code/{code}", null)).StatusCode);
        Badminton.Core.Utils.HanoiTime.NowUtc = () => _fixture.HanoiNowUtc.AddHours(3);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/v1/checkin/code/{code}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/v1/bookings/{id}/no-show", null)).StatusCode);
        Badminton.Core.Utils.HanoiTime.NowUtc = () => _fixture.HanoiNowUtc.AddHours(4);
        var completed = await _client.PostAsync($"/api/v1/bookings/{id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(100, (await completed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("pointsEarned").GetInt32());
    }

    [Fact]
    public async Task Http_password_recovery_requires_admin_reauthentication_and_invalidates_old_jwt()
    {
        const string adminPassword = "SyntheticRecoveryAdmin123";
        var adminId = await _fixture.RunScopeAsync(async sp => {
            var db = sp.GetRequiredService<BadmintonBookingDbContext>(); var admin = await db.Users.SingleAsync(u => u.Role == Role.Admin);
            admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword); await db.SaveChangesAsync(); return admin.Id;
        });
        ActAs(_fixture.Alice, "Customer");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync($"/api/v1/auth/recovery-code/{_fixture.Alice}", new { currentPassword = adminPassword, reason = "verified customer" })).StatusCode);
        ActAs(adminId, "Admin");
        var issued = await _client.PostAsJsonAsync($"/api/v1/auth/recovery-code/{_fixture.Alice}", new { currentPassword = adminPassword, reason = "verified customer at desk" });
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var code = (await issued.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("code").GetString();
        _client.DefaultRequestHeaders.Authorization = null;
        var request = new { phoneNumber = "0900000001", code, newPassword = "NewSyntheticPassword123" };
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/v1/auth/reset-password", request)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/auth/reset-password", request)).StatusCode);
        ActAs(_fixture.Alice, "Customer");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/account/profile")).StatusCode);
    }

    [Fact]
    public async Task Bounded_availability_read_load_stays_read_only_and_returns_no_errors()
    {
        var samples = new System.Collections.Concurrent.ConcurrentBag<double>();
        using var concurrency = new SemaphoreSlim(4);
        var before = await _fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.CountAsync());
        await Task.WhenAll(Enumerable.Range(0, 40).Select(async _ => {
            await concurrency.WaitAsync();
            try {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var response = await _client.GetAsync($"/api/v1/bookings/availability/{_fixture.BranchId}/{_fixture.FutureDate:yyyy-MM-dd}");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                samples.Add(timer.Elapsed.TotalMilliseconds);
            }
            finally { concurrency.Release(); }
        }));
        Assert.Equal(before, await _fixture.RunScopeAsync(sp => sp.GetRequiredService<BadmintonBookingDbContext>().Bookings.CountAsync()));
        var ordered = samples.Order().ToArray();
        Console.WriteLine($"BOUNDED_READ_LOAD requests=40 concurrency=4 errors=0 p50ms={ordered[19]:F1} p95ms={ordered[37]:F1} maxms={ordered[^1]:F1}");
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        await _fixture.DisposeAsync();
    }
}
