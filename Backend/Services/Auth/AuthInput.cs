using System.Text;
using System.Text.RegularExpressions;
using Badminton.Core.Base;

namespace Services;

public static partial class AuthInput
{
    [GeneratedRegex("^0[0-9]{9}$", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    public static string Phone(string phone)
    {
        var value = phone?.Trim() ?? "";
        if (!PhonePattern().IsMatch(value)) throw new BadRequestException("Số điện thoại phải có 10 chữ số, bắt đầu bằng 0.");
        return value;
    }

    public static void Password(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8 || Encoding.UTF8.GetByteCount(password) > 72)
            throw new BadRequestException("Mật khẩu tối thiểu 8 ký tự và tối đa 72 byte UTF-8.");
    }

    public static string Name(string name)
    {
        var value = name?.Trim() ?? "";
        if (value.Length is < 2 or > 100) throw new BadRequestException("Họ tên phải từ 2 đến 100 ký tự.");
        return value;
    }
}
