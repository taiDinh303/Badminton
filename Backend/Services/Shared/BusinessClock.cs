using Badminton.Core.Time;
using Badminton.Core.Utils;
namespace Services;

public sealed class BusinessClock : IBusinessClock
{
    // UTC deadlines use elapsed system time; HanoiTime is the legacy business-date test seam.
    public DateTime UtcNow => DateTime.UtcNow;
    public DateTime LocalNow => HanoiTime.HanoiNow();
    public DateOnly Today => HanoiTime.HanoiToday();
}
