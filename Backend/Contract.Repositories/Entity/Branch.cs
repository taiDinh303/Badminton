using Badminton.Core.Base;
using Badminton.Core.Enums;

namespace Contract.Repositories.Entity
{
    public class Branch : NamedIntMasterEntity
    {
        public string Address { get; set; } = null!;
        public double? GeoLat { get; set; }
        public double? GeoLng { get; set; }
        public TimeOnly OpenTime { get; set; } = new(6, 0);
        public TimeOnly CloseTime { get; set; } = new(23, 0);
        public BranchStatus Status { get; set; } = BranchStatus.Active;
    }
}
