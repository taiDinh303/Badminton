using Badminton.Core.Security;

namespace Services;

public sealed class LoginAttemptTracker(TimeProvider? timeProvider = null) : ILoginAttemptTracker
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly object _gate = new();
    private readonly Dictionary<string, AttemptWindow> _entries = new(StringComparer.Ordinal);
    private const int MaxEntries = 10_000;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);
    private sealed record AttemptWindow(int Count, DateTimeOffset Start, DateTimeOffset? LockedUntil);

    public bool IsLocked(string phone)
    {
        lock (_gate)
            return _entries.TryGetValue(phone, out var entry) && entry.LockedUntil > _time.GetUtcNow();
    }

    public void RecordFailure(string phone)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            foreach (var key in _entries.Where(p => p.Value.LockedUntil is { } until
                         ? until <= now : p.Value.Start + Window <= now).Select(p => p.Key).ToArray())
                _entries.Remove(key);
            if (!_entries.TryGetValue(phone, out var entry))
            {
                if (_entries.Count >= MaxEntries) return;
                entry = new AttemptWindow(0, now, null);
            }
            if (entry.LockedUntil > now) return;
            var count = entry.Count + 1;
            _entries[phone] = entry with { Count = count, LockedUntil = count >= 5 ? now + Lockout : null };
        }
    }

    public void Clear(string phone) { lock (_gate) _entries.Remove(phone); }
}
