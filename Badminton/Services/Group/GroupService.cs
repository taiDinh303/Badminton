using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Group;
using M.Core.Base;
using Microsoft.AspNetCore.Http;
using ModelViews.Group;

namespace Services
{
    public sealed class GroupService : BaseService, IGroupService
    {
        public GroupService(IUnitOfWork uow, IHttpContextAccessor http) : base(uow, http) { }

        private IGenericRepository<PlayGroup> Repo => Repository<PlayGroup>();

        public async Task<IReadOnlyList<GroupView>> GetByOwnerAsync(int ownerId)
        {
            var groups = await Repo.GetByConditionAsync(g => g.OwnerId == ownerId && g.DeletedAt == null);
            var memberCounts = new Dictionary<int, int>();
            foreach (var g in groups)
            {
                var members = await Repository<GroupMember>().GetByConditionAsync(m => m.GroupId == g.Id);
                memberCounts[g.Id] = members.Count;
            }
            return groups.Select(g => new GroupView(
                g.Id, g.Name, g.OwnerId, OwnerNamePlaceholder, g.MaxMembers,
                memberCounts.TryGetValue(g.Id, out var c) ? c : 0, g.CreatedAt)).ToList();
        }

        public async Task<GroupView> CreateAsync(string name, int ownerId, int maxMembers = 10)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new BadRequestException("TÃªn nhÃ³m khÃ´ng Ä‘Æ°á»£c rá»—ng.");
            if (maxMembers < 1 || maxMembers > 10)
                throw new BadRequestException("NhÃ³m tá»‘i Ä‘a 10 thÃ nh viÃªn (docs 01 Â§4).");

            var group = new PlayGroup { Name = name, OwnerId = ownerId, MaxMembers = maxMembers };
            _ = Repo.AddAsync(group);

            _ = Repository<GroupMember>().AddAsync(new GroupMember
            {
                GroupId = 0, // set after save
                UserId = ownerId,
                Group = group
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
                throw new ConflictException($"NhÃ³m Ä‘Ã£ Ä‘á»§ {g.MaxMembers} thÃ nh viÃªn.");

            g.Members.Add(new GroupMember { UserId = userId, GroupId = groupId, Group = g });
            await SaveAsync();
        }

        public async Task<IReadOnlyList<GroupMemberView>> GetMembersAsync(int groupId)
        {
            await MustGetAsync(groupId);
            var members = await Repository<GroupMember>().GetByConditionAsync(m => m.GroupId == groupId);
            var views = new List<GroupMemberView>();
            foreach (var m in members)
            {
                var u = await Repository<User>().GetByIdAsync(m.UserId);
                views.Add(new GroupMemberView(m.UserId, u?.FullName ?? "?",
                    u?.PhoneNumber ?? "?", m.UserId == (await MustGetAsync(groupId)).OwnerId, m.JoinedAt));
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
