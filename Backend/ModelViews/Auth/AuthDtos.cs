namespace ModelViews.Auth
{
    public record RegisterRequest(string FullName, string PhoneNumber, string Password,
        string? Email = null, RoleDto Role = RoleDto.Customer);

    public enum RoleDto { Customer, Manager, Admin }

    public record LoginRequest(string PhoneNumber, string Password, string? Email = null);

    public record LoginResponse(int UserId, string FullName, string PhoneNumber,
        string Role, string AccessToken, int ExpiresInMinutes);
}
