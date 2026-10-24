using System.ComponentModel.DataAnnotations;

namespace ModelViews.Auth
{
    public record RegisterRequest([Required, StringLength(100, MinimumLength = 2)] string FullName,
        [Required, RegularExpression("^0[0-9]{9}$")] string PhoneNumber,
        [Required, StringLength(72, MinimumLength = 8)] string Password,
        [EmailAddress, StringLength(150)] string? Email = null, RoleDto Role = RoleDto.Customer);

    public enum RoleDto { Customer, Manager, Admin }

    public record LoginRequest([Required] string PhoneNumber, [Required] string Password, string? Email = null);

    public record ChangePasswordRequest([Required] string CurrentPassword,
        [Required, StringLength(72, MinimumLength = 8)] string NewPassword);

    public record LoginResponse(int UserId, string FullName, string PhoneNumber,
        string Role, string AccessToken, int ExpiresInMinutes);
}
