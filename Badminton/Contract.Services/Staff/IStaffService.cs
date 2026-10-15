using M.Core.Enums;

namespace Contract.Services.Staff
{
    public interface IStaffService
    {
        /// <summary>Create a Manager or Admin. Admin-only. Enforces the
        /// "exactly one active Admin" invariant (docs 01 §2) via the
        /// filtered unique index + a service-level check.</summary>
        Task<int> CreateAsync(StaffCreateRequest request, int actorId);

        /// <summary>Lock / unlock an account. Admin-only. Sets
        /// <see cref="UserStatus"/></summary>
        Task SetStatusAsync(int userId, UserStatus status, int actorId);

        /// <summary>Change a user's role (Admin only). Manager may not be
        /// promoted to Admin directly by another Manager.</summary>
        Task SetRoleAsync(int userId, Role role, int actorId);

        /// <summary>Assign (or reassign) a Manager to a branch. A Manager
        /// must belong to exactly one branch for branch-scope to work.</summary>
        Task AssignBranchAsync(int userId, int branchId, int actorId);

        /// <summary>List staff (Manager + Admin), newest first.</summary>
        Task<IReadOnlyList<StaffView>> ListAsync();
    }

    public sealed record StaffCreateRequest(string FullName, string PhoneNumber,
        string Password, string? Email = null, Role Role = Role.Manager,
        int? BranchId = null);

    public sealed record StaffView(int Id, string FullName, string PhoneNumber,
        string Role, string Status, int? BranchId, bool IsVerified, DateTime CreatedAt);
}
