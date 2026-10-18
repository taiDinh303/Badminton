namespace Badminton.Core.Store
{
    public class NotifySettings
    {
        public string Channels { get; set; } = "log";

        public string[] ChannelList =>
            Channels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // ── Email (generic HTTP mail relay — Send-Email API) ─────────
        public string EmailEndpoint { get; set; } = string.Empty;
        public string EmailKey { get; set; } = string.Empty;
        public string FromAddress { get; set; } = "badminton@example.com";

        // ── SMS (provider-agnostic HTTP API) ─────────────────────────
        public string SmsEndpoint { get; set; } = string.Empty;
        public string SmsKey { get; set; } = string.Empty;

        // ── Zalo OA (webhook) ────────────────────────────────────────
        public string ZaloEndpoint { get; set; } = string.Empty;
        public string ZaloToken { get; set; } = string.Empty;
    }
}
