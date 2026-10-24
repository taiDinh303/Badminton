namespace Badminton.Core.Security;

public interface ILoginAttemptTracker
{
    bool IsLocked(string phone);
    void RecordFailure(string phone);
    void Clear(string phone);
}
