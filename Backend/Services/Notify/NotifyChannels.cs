using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Contract.Repositories.Entity;
using Contract.Services.Notify;
using Badminton.Core.Enums;
using Badminton.Core.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Services
{
    public sealed class LogNotifyChannel : INotifyChannel
    {
        private readonly ILogger _log;
        public string Name => "log";
        public bool IsConfigured => true;
        public LogNotifyChannel(ILogger<LogNotifyChannel> log) => _log = log;
        public Task SendAsync(User user, NotifyEvent evt, string body)
        {
            _log.LogInformation("[notify:{Channel}] user={User} event={Event} body={Body}",
                Name, user.Id, evt, body);
            return Task.CompletedTask;
        }
    }

    public sealed class EmailNotifyChannel : INotifyChannel
    {
        private readonly NotifySettings _s;
        private readonly HttpClient _http;
        private readonly ILogger _log;
        public string Name => "email";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_s.EmailEndpoint);
        public EmailNotifyChannel(IOptions<NotifySettings> opts, IHttpClientFactory http,
            ILogger<EmailNotifyChannel> log)
        {
            _s = opts.Value;
            _http = http.CreateClient("notify-email");
            _log = log;
        }
        public async Task SendAsync(User user, NotifyEvent evt, string body)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _log.LogWarning("[notify:email] user={User} has no email; skipping.", user.Id);
                return;
            }
            if (!string.IsNullOrWhiteSpace(_s.EmailKey))
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _s.EmailKey);
            var payload = new
            {
                to = user.Email,
                from = _s.FromAddress,
                subject = $"BadmintonBooking – {evt}",
                text = body
            };
            var resp = await _http.PostAsJsonAsync(_s.EmailEndpoint, payload);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Email HTTP {resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
        }
    }

    public sealed class SmsNotifyChannel : INotifyChannel
    {
        private readonly NotifySettings _s;
        private readonly HttpClient _http;
        private readonly ILogger _log;
        public string Name => "sms";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_s.SmsEndpoint);
        public SmsNotifyChannel(IOptions<NotifySettings> opts, IHttpClientFactory http,
            ILogger<SmsNotifyChannel> log)
        {
            _s = opts.Value;
            _http = http.CreateClient("notify-sms");
            _log = log;
        }
        public async Task SendAsync(User user, NotifyEvent evt, string body)
        {
            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                _log.LogWarning("[notify:sms] user={User} has no phone; skipping.", user.Id);
                return;
            }
            if (!string.IsNullOrWhiteSpace(_s.SmsKey))
                _http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _s.SmsKey);
            var payload = new { to = user.PhoneNumber, message = body };
            var resp = await _http.PostAsJsonAsync(_s.SmsEndpoint, payload);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"SMS HTTP {resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
        }
    }

    public sealed class ZaloNotifyChannel : INotifyChannel
    {
        private readonly NotifySettings _s;
        private readonly HttpClient _http;
        private readonly ILogger _log;
        public string Name => "zalo";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_s.ZaloEndpoint);
        public ZaloNotifyChannel(IOptions<NotifySettings> opts, IHttpClientFactory http,
            ILogger<ZaloNotifyChannel> log)
        {
            _s = opts.Value;
            _http = http.CreateClient("notify-zalo");
            _log = log;
        }
        public async Task SendAsync(User user, NotifyEvent evt, string body)
        {
            var url = _s.ZaloEndpoint
                + (string.IsNullOrWhiteSpace(_s.ZaloToken) ? "" :
                   $"?token={Uri.EscapeDataString(_s.ZaloToken)}");
            var payload = new { to = user.PhoneNumber, text = body };
            var resp = await _http.PostAsJsonAsync(url, payload);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Zalo HTTP {resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
        }
    }
}
