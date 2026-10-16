using Badminton.Core.Base;

namespace Contract.Repositories.Entity
{
    public class TimeSlot : IntBaseEntity
    {
        public string Label { get; set; } = null!;
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public bool IsPeak { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
