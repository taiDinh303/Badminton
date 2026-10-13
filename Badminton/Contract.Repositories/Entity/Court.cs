using M.Core.Base;
using M.Core.Enums;

namespace Contract.Repositories.Entity
{
    /// <summary>
    /// A physical badminton court. INHERITANCE: extends
    /// <see cref="IntBaseEntity"/> for the audit/soft-delete fields.
    /// Holds a reference to its <see cref="CourtType"/>.
    /// </summary>
    public class Court : IntBaseEntity
    {
        public int CourtTypeId { get; set; }
        public CourtType? CourtType { get; set; }

        public string CourtCode { get; set; } = null!;
        public string CourtName { get; set; } = null!;
        public string? Description { get; set; }
        public string? Location { get; set; }

        /// <summary>Base hourly price (the fee policy may add surcharges on top).</summary>
        public decimal PricePerHour { get; set; }

        public CourtStatus Status { get; set; } = CourtStatus.Available;
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
