using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Badminton.API.Configuration;
using Badminton.Core.Store;
using Contract.Repositories.Entity;
using Contract.Services.Contact;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelViews.Contact;
using Services;
using Xunit;

namespace Badminton.Tests;

// Real HTTP pipeline and JWT authorization, with an isolated inbox double (no live DB).
public sealed class ContactHttpTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly Inbox _inbox = new();
    private readonly JwtSettings _jwt = new() { Key = new string('t', 64), Issuer = "ContactTests", Audience = "ContactTests", ExpireMinutes = 10 };

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(Badminton.API.Program).Assembly.GetName().Name, EnvironmentName = "Testing" });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:BadmintonBooking"] = "Server=127.0.0.1;Database=UnusedContactTest;Integrated Security=true;Encrypt=true",
            ["JwtSettings:Key"] = _jwt.Key, ["JwtSettings:Issuer"] = _jwt.Issuer,
            ["JwtSettings:Audience"] = _jwt.Audience, ["JwtSettings:ExpireMinutes"] = "10",
            ["BackgroundJobs:Enabled"] = "false", ["Payment:Provider"] = "mock",
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddBookingApi(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton<IContactService>(_inbox);
        _app = builder.Build();
        _app.UseRouting(); _app.UseAuthentication(); _app.UseRateLimiter(); _app.UseAuthorization(); _app.MapControllers();
        await _app.StartAsync();
        _client = new HttpClient { BaseAddress = new Uri(_app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
    }

    private static ContactRequest Valid() => new() { FullName = "Guest Example", Phone = "0901234567", Email = "guest@example.test", Consent = true };
    private void ActAs(string role) => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtGenerator(Options.Create(_jwt)).Generate(1, "Contact Test", "0900000000", role).access);

    [Fact]
    public async Task Guest_can_submit_but_cannot_read_inbox()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/v1/contacts", Valid())).StatusCode);
        Assert.Equal(1, _inbox.Created);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/contacts")).StatusCode);
    }

    [Fact]
    public async Task Validation_rejects_missing_consent_invalid_phone_and_email()
    {
        var request = Valid(); request.Consent = false;
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/contacts", request)).StatusCode);
        request = Valid(); request.Phone = "123";
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/contacts", request)).StatusCode);
        request = Valid(); request.Email = "invalid";
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/contacts", request)).StatusCode);
        Assert.Equal(0, _inbox.Created);
    }

    [Theory]
    [InlineData("Customer", HttpStatusCode.Forbidden)]
    [InlineData("Manager", HttpStatusCode.Forbidden)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task Only_admin_can_read_and_handle(string role, HttpStatusCode expected)
    {
        ActAs(role);
        Assert.Equal(expected, (await _client.GetAsync("/api/v1/contacts")).StatusCode);
        Assert.Equal(expected, (await _client.PatchAsync("/api/v1/contacts/1/handled", null)).StatusCode);
    }

    [Fact]
    public async Task Public_submission_is_rate_limited()
    {
        for (var i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/v1/contacts", Valid())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await _client.PostAsJsonAsync("/api/v1/contacts", Valid())).StatusCode);
    }

    public async Task DisposeAsync() { _client.Dispose(); await _app.DisposeAsync(); }

    private sealed class Inbox : IContactService
    {
        public int Created { get; private set; }
        public Task CreateAsync(ContactRequest request, CancellationToken ct) { Created++; return Task.CompletedTask; }
        public Task<(IReadOnlyList<ContactInquiry> Items, int TotalCount)> ListAsync(int page, CancellationToken ct) => Task.FromResult<(IReadOnlyList<ContactInquiry>, int)>((Array.Empty<ContactInquiry>(), 0));
        public Task<bool> MarkHandledAsync(int id, CancellationToken ct) => Task.FromResult(true);
    }
}
