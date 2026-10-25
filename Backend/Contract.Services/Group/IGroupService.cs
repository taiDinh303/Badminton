using ModelViews.Group;

namespace Contract.Services.Group
{
    public interface IGroupService
    {
        Task<IReadOnlyList<GroupView>> GetByOwnerAsync(int ownerId);
        Task<GroupView> CreateAsync(string name, int ownerId, int maxMembers = 10);
        Task JoinAsync(int groupId, int userId, string? inviteCode = null);
        Task<IReadOnlyList<GroupView>> GetJoinedAsync(int userId);
        Task LeaveAsync(int groupId, int userId);
        Task RemoveMemberAsync(int groupId, int memberId, int ownerId);
        Task RenameAsync(int groupId, string name, int ownerId);
        Task DisbandAsync(int groupId, int ownerId);
        Task TransferAsync(int groupId, int newOwnerId, int ownerId);
        Task<string> RotateInviteAsync(int groupId, int ownerId);
        Task<IReadOnlyList<GroupMemberView>> GetMembersAsync(int groupId);
        Task<bool> CanBookForGroupAsync(int groupId, int userId);
    }
}
