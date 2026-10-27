using Badminton.Core.Base;
namespace Services.Operations;

internal static class ClubInput
{
    public static string Text(string? value, int max, string label)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0 || text.Length > max || text.Any(char.IsControl))
            throw new BadRequestException($"{label}: nhập từ 1 đến {max} ký tự, không chứa ký tự điều khiển.");
        return text;
    }
    public static void Range(DateTime from, DateTime to, TimeSpan max)
    {
        if (from.Kind != DateTimeKind.Utc || to.Kind != DateTimeKind.Utc || to <= from || to - from > max)
            throw new BadRequestException("Khoảng giờ UTC không hợp lệ.");
    }
    public static void Number(int n, int min, int max, string label)
    {
        if (n < min || n > max)
            throw new BadRequestException($"{label}: từ {min} đến {max}.");
    }
}
