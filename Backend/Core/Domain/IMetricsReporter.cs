namespace Badminton.Core.Domain
{
    public interface IMetricsReporter
    {
        void CountWebhook(string provider, string outcome);
        void CountNotify(string evt, string channel, string outcome);
        void CountJobLockAcquired(string job);
    }

    public sealed class NoopMetricsReporter : IMetricsReporter
    {
        public static readonly NoopMetricsReporter Instance = new();
        private NoopMetricsReporter() { }
        public void CountWebhook(string provider, string outcome) { }
        public void CountNotify(string evt, string channel, string outcome) { }
        public void CountJobLockAcquired(string job) { }
    }
}
