using Badminton.Core.Base;
using Badminton.Core.Enums;

namespace Contract.Repositories.Entity
{
    public class Court : IntBaseEntity
    {
        public int BranchId { get; set; }
        public Branch? Branch { get; set; }

        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public CourtSurface Surface { get; set; } = CourtSurface.Sindalo;

        /// <summary>VND/hour, integer.</summary>
        public int PriceLowPeak { get; set; }
        public int PricePeak { get; set; }

        /// <summary>Weekend surcharge as a fraction of peak price (0.20 = +20%).</summary>
        public decimal WeekendSurcharge { get; set; } = 0.20m;

        public int Capacity { get; set; } = 2;

        /// <summary>JSON array of amenity strings.</summary>
        public string? Amenities { get; set; }
        public string? ImageUrl { get; set; }

        public double? RatingAvg { get; set; }
        public int RatingCount { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
