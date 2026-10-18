using Badminton.Core.Domain;
using Badminton.API.Metrics;

namespace Badminton.API
{
    public sealed class PrometheusMetricsReporter : IMetricsReporter
    {
        public void CountWebhook(string provider, string outcome) =>
            BadmintonMetrics.CountWebhook(provider, outcome);

        public void CountNotify(string evt, string channel, string outcome) =>
            BadmintonMetrics.CountNotify(evt, channel, outcome);

        public void CountJobLockAcquired(string job) =>
            BadmintonMetrics.CountJobLockAcquired(job);
    }
}
