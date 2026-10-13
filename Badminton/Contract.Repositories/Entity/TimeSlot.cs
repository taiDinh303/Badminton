using M.Core.Base;

namespace Contract.Repositories.Entity
{
    /// <summary>
    /// A bookable hour-of-day slot (e.g. 08:00-09:00).
    /// INHERITANCE: extends <see cref="IntBaseEntity"/>.
    /// </summary>
    public class TimeSlot : IntBaseEntity
    {
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
