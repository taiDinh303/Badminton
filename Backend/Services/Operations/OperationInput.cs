using Badminton.Core.Base;
namespace Services.Operations;

internal static class OperationInput
{
    public static string Text(string? value, int max = 2000, int min = 5)
    {
        var text = value?.Trim() ?? "";
        if (text.Length < min || text.Length > max)
            throw new BadRequestException($"Nội dung cần từ {min} đến {max} ký tự.");
        return text;
    }
    public static void DateRange(DateOnly from, DateOnly to, int maxDays = 31)
    {
        if (to < from || to.DayNumber - from.DayNumber > maxDays)
            throw new BadRequestException($"Chọn khoảng thời gian không quá {maxDays + 1} ngày.");
    }
    public static T EnumValue<T>(string value) where T : struct, Enum
        => Enum.TryParse<T>(value, true, out var result) && Enum.IsDefined(result)
            ? result : throw new BadRequestException("Loại yêu cầu không hợp lệ.");
}
