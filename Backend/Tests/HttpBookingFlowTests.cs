using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Badminton.API.Configuration;
using Badminton.API.Middleware;
using Badminton.Core.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;
using Services;
using Xunit;

namespace Badminton.Tests;

public sealed class HttpBookingFlowTests : IAsyncLifetime
{
    private readonly TestHost _fixture = new();
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

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null) await _app.DisposeAsync();
        await _fixture.DisposeAsync();
    }
}
