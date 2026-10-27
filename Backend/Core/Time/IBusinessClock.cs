namespace Badminton.Core.Time;

public interface IBusinessClock
{
    DateTime UtcNow
    {
        get;
    }
    DateTime LocalNow
    {
        get;
    }
    DateOnly Today
    {
        get;
    }
}
