namespace ModelViews.Auth
{
    public record RegisterRequest(string FullName, string PhoneNumber, string Password,
        string? Email = null, RoleDto Role = RoleDto.Customer);

    /// <summary>DTO-side role enum so the request never imports core types.</summary>
    public enum RoleDto { Customer, Manager, Admin }

    /// <summary>Phone + password (dev) login. OTP login = Supabase Auth token.</summary>
    public record LoginRequest(string PhoneNumber, string Password, string? Email = null);

    public record LoginResponse(int UserId, string FullName, string PhoneNumber,
        string Role, string AccessToken, int ExpiresInMinutes);
}
