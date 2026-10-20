using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Group;
using Badminton.Core.Base;
using Badminton.Core.Security;
using ModelViews.Group;

namespace Services
{
    public sealed class GroupService : BaseService, IGroupService
    {
        public GroupService(IUnitOfWork uow, ICurrentUser currentUser) : base(uow, currentUser) { }

        private IGenericRepository<PlayGroup> Repo => Repository<PlayGroup>();

        public async Task<IReadOnlyList<GroupView>> GetByOwnerAsync(int ownerId)
        {
            var groups = await Repo.GetByConditionAsync(g => g.OwnerId == ownerId && g.DeletedAt == null);
            if (groups.Count == 0) return Array.Empty<GroupView>();

            var groupIds = groups.Select(g => g.Id).ToList();
            var members = await Repository<GroupMember>().GetByConditionAsync(m => groupIds.Contains(m.GroupId));
            var counts = members
                .GroupBy(m => m.GroupId)
                .ToDictionary(g => g.Key, g => g.Count());

            return groups.Select(g => new GroupView(
                g.Id, g.Name, g.OwnerId, OwnerNamePlaceholder, g.MaxMembers,
                counts.TryGetValue(g.Id, out var c) ? c : 0, g.CreatedAt)).ToList();
        }

        public async Task<GroupView> CreateAsync(string name, int ownerId, int maxMembers = 10)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new BadRequestException("Ten nhom khong duoc rong.");
            if (maxMembers < 1 || maxMembers > 10)
                throw new BadRequestException("Nhom toi da 10 thanh vien (docs 01 §4).");

            var group = new PlayGroup { Name = name, OwnerId = ownerId, MaxMembers = maxMembers };
            _ = Repo.AddAsync(group);
            await SaveAsync();

            _ = Repository<GroupMember>().AddAsync(new GroupMember
            {
                GroupId = group.Id,
                UserId = ownerId
            });
            await SaveAsync();
            return new GroupView(group.Id, group.Name, ownerId, OwnerNamePlaceholder,
                group.MaxMembers, 1, group.CreatedAt);
        }

        public async Task JoinAsync(int groupId, int userId)
        {
            var g = await MustGetAsync(groupId);
            var members = await Repository<GroupMember>().GetByConditionAsync(m => m.GroupId == groupId);
            if (members.Any(m => m.UserId == userId)) return; // already a member
            if (members.Count >= g.MaxMembers)
                throw new ConflictException($"Nhom da dat {g.MaxMembers} thanh vien.");

            g.Members.Add(new GroupMember { UserId = userId, GroupId = groupId, Group = g });
            await SaveAsync();
        }

        public async Task<IReadOnlyList<GroupMemberView>> GetMembersAsync(int groupId)
        {
            var g = await MustGetAsync(groupId);
            var members = await Repository<GroupMember>().GetByConditionAsync(m => m.GroupId == groupId);

            // ANTI-N+1: bulk-load the user names, then map in memory.
            var userIds = members.Select(m => m.UserId).Distinct().ToList();
            var users = (await Repository<User>().GetByConditionAsync(u => userIds.Contains(u.Id)))
                .ToDictionary(u => u.Id);

            var views = new List<GroupMemberView>(members.Count);
            foreach (var m in members)
            {
                users.TryGetValue(m.UserId, out var u);
                views.Add(new GroupMemberView(m.UserId, u?.FullName ?? "?",
                    u?.PhoneNumber ?? "?", m.UserId == g.OwnerId, m.JoinedAt));
            }
            return views;
        }

        public async Task<bool> CanBookForGroupAsync(int groupId, int userId)
        {
            var g = await MustGetAsync(groupId);
            return g.OwnerId == userId;
        }

        private static string OwnerNamePlaceholder => "(owner)";

        private async Task<PlayGroup> MustGetAsync(int id)
        {
            var g = await Repo.GetByIdAsync(id);
            if (g is null || g.DeletedAt.HasValue) throw new NotFoundException("PlayGroup", id);
            return g;
        }
    }
}
