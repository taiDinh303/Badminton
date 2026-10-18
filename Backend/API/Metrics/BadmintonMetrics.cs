using Prometheus;

namespace Badminton.API.Metrics
{
    public static class BadmintonMetrics
    {
        private static readonly Prometheus.Counter WebhooksReceived =
            Prometheus.Metrics.CreateCounter(
                "badminton_webhooks_received_total",
                "Gateway webhook deliveries (verified / rejected / duplicate).",
                new[] { "provider", "outcome" });

        private static readonly Prometheus.Counter NotifyMessages =
            Prometheus.Metrics.CreateCounter(
                "badminton_notify_messages_total",
                "Outbox notify fan-out outcomes (delivered / failed / skipped).",
                new[] { "event", "channel", "outcome" });

        private static readonly Prometheus.Counter JobLockAcquired =
            Prometheus.Metrics.CreateCounter(
                "badminton_job_lock_acquired_total",
                "Number of JobLock acquire attempts that succeeded (per job).",
                new[] { "job" });

        public static void CountWebhook(string provider, string outcome)
        {
            WebhooksReceived.WithLabels(provider, outcome).Inc();
        }

        public static void CountNotify(string evt, string channel, string outcome)
        {
            NotifyMessages.WithLabels(evt, channel, outcome).Inc();
        }

        public static void CountJobLockAcquired(string job)
        {
            JobLockAcquired.WithLabels(job).Inc();
        }
    }
}
