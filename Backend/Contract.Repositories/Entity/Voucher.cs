using Badminton.Core.Base;
using Badminton.Core.Enums;

namespace Contract.Repositories.Entity
{
    public class Voucher : IntBaseEntity
    {
        public string Code { get; set; } = null!;
        public VoucherType Type { get; set; }
        public int Value { get; set; }
        public int? MaxValue { get; set; }
        public int MinSpend { get; set; }
        public int MaxPerUser { get; set; } = 1;
        public int TotalQty { get; set; }
        public int UsedQty { get; set; }
        public DateOnly ValidFrom { get; set; }
        public DateOnly? ValidUntil { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
