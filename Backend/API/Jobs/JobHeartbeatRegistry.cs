using System.Collections.Concurrent;

namespace Badminton.API.Jobs
{
    public sealed class JobHeartbeatRegistry
    {
        private readonly ConcurrentDictionary<string, DateTime> _last = new();

        public void Record(string job) => _last[job] = DateTime.UtcNow;

        public DateTime? Last(string job)
            => _last.TryGetValue(job, out var t) ? t : null;

        public IReadOnlyDictionary<string, DateTime> Snapshot()
            => _last.ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
