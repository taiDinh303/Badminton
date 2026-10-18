using ModelViews.Group;

namespace Contract.Services.Group
{
    public interface IGroupService
    {
        Task<IReadOnlyList<GroupView>> GetByOwnerAsync(int ownerId);
        Task<GroupView> CreateAsync(string name, int ownerId, int maxMembers = 10);
        Task JoinAsync(int groupId, int userId);
        Task<IReadOnlyList<GroupMemberView>> GetMembersAsync(int groupId);
        Task<bool> CanBookForGroupAsync(int groupId, int userId);
    }
}
