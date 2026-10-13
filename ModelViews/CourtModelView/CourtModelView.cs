using Core.Enums;

namespace ModelViews.CourtModelView
{
    /// <summary>
    /// DTO trả về cho client – thể hiện được cả sân trong nhà lẫn ngoài trời
    /// (đa hình qua trường <see cref="Category"/> và các thuộc tính mở rộng).
    /// </summary>
    public class CourtResponseModelView
    {
        public Guid Id { get; set; }
        public string CourtName { get; set; } = string.Empty;
        public CourtCategory Category { get; set; }
        public int OpenTime { get; set; }
        public int CloseTime { get; set; }
        public int UnitPrice { get; set; }
        public string? Description { get; set; }

        // Thuộc tính riêng cho IndoorCourt
        public string? FloorMaterial { get; set; }
        public bool HasAirConditioning { get; set; }
        public int LightLevel { get; set; }

        // Thuộc tính riêng cho OutdoorCourt
        public bool HasCanopy { get; set; }
        public string? WeatherNote { get; set; }

        public DateTimeOffset CreatedTime { get; set; }
    }

    /// <summary>
    /// DTO tạo sân – client chọn loại (Indoor/Outdoor), service đa hình tạo đúng entity.
    /// </summary>
    public class CreateCourtModelView
    {
        public CourtCategory Category { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string CourtName { get; set; } = string.Empty;

        public int OpenTime { get; set; } = 6;
        public int CloseTime { get; set; } = 22;

        [System.ComponentModel.DataAnnotations.Required]
        public int UnitPrice { get; set; }

        public string? Description { get; set; }

        // Cho Indoor
        public string? FloorMaterial { get; set; }
        public bool HasAirConditioning { get; set; }
        public int LightLevel { get; set; } = 100;

        // Cho Outdoor
        public bool HasCanopy { get; set; }
        public string? WeatherNote { get; set; }
    }

    /// <summary>DTO cập nhật sân.</summary>
    public class UpdateCourtModelView
    {
        public Guid Id { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string CourtName { get; set; } = string.Empty;

        public int OpenTime { get; set; }
        public int CloseTime { get; set; }
        public int UnitPrice { get; set; }
        public string? Description { get; set; }
    }
}
