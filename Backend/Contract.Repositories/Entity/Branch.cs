using Badminton.Core.Base;
using Badminton.Core.Enums;

namespace Contract.Repositories.Entity
{
    /// <c>open_time/close_time</c> are the default slot range (06:00–23:00,
    /// step 1h → 17 slots/day; configurable with audit.</summary>
    public class Branch : NamedIntMasterEntity
    {
        public string Address { get; set; } = null!;
        public double? GeoLat { get; set; }
        public double? GeoLng { get; set; }

        /// <summary>Default opening window; slots are generated from it.</summary>
        public TimeOnly OpenTime { get; set; } = new(6, 0);
        public TimeOnly CloseTime { get; set; } = new(23, 0);

        public BranchStatus Status { get; set; } = BranchStatus.Active;
    }
}
