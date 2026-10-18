using Badminton.Core.Enums;

namespace Contract.Services.Staff
{
    public interface IStaffService
    {
        Task<int> CreateAsync(StaffCreateRequest request, int actorId);
        Task SetStatusAsync(int userId, UserStatus status, int actorId);
        Task SetRoleAsync(int userId, Role role, int actorId);
        Task AssignBranchAsync(int userId, int branchId, int actorId);
        Task<IReadOnlyList<StaffView>> ListAsync();
    }

    public sealed record StaffCreateRequest(string FullName, string PhoneNumber,
        string Password, string? Email = null, Role Role = Role.Manager,
        int? BranchId = null);

    public sealed record StaffView(int Id, string FullName, string PhoneNumber,
        string Role, string Status, int? BranchId, bool IsVerified, DateTime CreatedAt);
}
