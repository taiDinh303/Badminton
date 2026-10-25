using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Group;
using Badminton.Core.Base;
using Badminton.Core.Security;
using ModelViews.Group;
using Contract.Services.Audit;
using System.Security.Cryptography;
using System.Text;

namespace Services
{
    public sealed class GroupService : BaseService, IGroupService
    {
        private readonly IAuditService _audit;
        public GroupService(IUnitOfWork uow, ICurrentUser currentUser, IAuditService audit) : base(uow, currentUser) { _audit = audit; }

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
            GroupView result = null!;
            await Uow.RunInTransactionAsync(async () => result = await CreateCoreAsync(name, ownerId, maxMembers));
            return result;
        }

        private async Task<GroupView> CreateCoreAsync(string name, int ownerId, int maxMembers)
        {
            await RequireActiveAsync(ownerId);
            if (name?.Trim().Length > 100) throw new BadRequestException("Tên nhóm tối đa 100 ký tự.");
            if (string.IsNullOrWhiteSpace(name))
                throw new BadRequestException("Ten nhom khong duoc rong.");
            if (maxMembers < 1 || maxMembers > 10)
                throw new BadRequestException("Nhom toi da 10 thanh vien.");

            var invite = NewInvite();
            var group = new PlayGroup { Name = name.Trim(), OwnerId = ownerId, MaxMembers = maxMembers, InviteTokenHash = Hash(invite) };
            group.Members.Add(new GroupMember { UserId = ownerId, Group = group });
            await Repo.AddAsync(group);
            await SaveAsync();
            await _audit.LogAsync(ownerId, "group.create", "PlayGroup", group.Id.ToString(), null, new { group.Name, group.OwnerId, group.MaxMembers });
            return new GroupView(group.Id, group.Name, ownerId, OwnerNamePlaceholder,
                group.MaxMembers, 1, group.CreatedAt, invite);
        }

        public Task JoinAsync(int groupId, int userId, string? inviteCode = null) => Uow.RunInTransactionAsync(async () =>
        {
            await RequireActiveAsync(userId);
            await Uow.LockGroupAsync(groupId);
            await JoinCoreAsync(groupId, userId, inviteCode);
        });

        private async Task JoinCoreAsync(int groupId, int userId, string? inviteCode)
        {
            var g = await MustGetAsync(groupId);
            var members = await Repository<GroupMember>().GetByConditionAsync(m => m.GroupId == groupId);
            if (members.Any(m => m.UserId == userId)) return; // already a member
            if (g.InviteTokenHash is not null && !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(g.InviteTokenHash), Encoding.ASCII.GetBytes(Hash(inviteCode ?? ""))))
                throw new ConflictException("Mã mời không hợp lệ hoặc đã bị thu hồi.");
            if (members.Count >= g.MaxMembers)
                throw new ConflictException($"Nhom da dat {g.MaxMembers} thanh vien.");

            g.Members.Add(new GroupMember { UserId = userId, GroupId = groupId, Group = g });
            await SaveAsync();
            await _audit.LogAsync(userId, "group.join", "PlayGroup", groupId.ToString(), null, new { UserId = userId });
        }

        public async Task<IReadOnlyList<GroupMemberView>> GetMembersAsync(int groupId)
        {
            var g = await MustGetAsync(groupId);
            await RequireActiveAsync(_currentUser.Id);
            var members = await Repository<GroupMember>().GetByConditionAsync(m => m.GroupId == groupId);
            if (g.OwnerId != _currentUser.Id && !members.Any(m => m.UserId == _currentUser.Id))
                throw new ConflictException("Chỉ thành viên của nhóm được xem danh sách thành viên.");

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

        public async Task<IReadOnlyList<GroupView>> GetJoinedAsync(int userId)
        {
            var ids = (await Repository<GroupMember>().GetByConditionAsync(m => m.UserId == userId)).Select(m => m.GroupId).ToList();
            var groups = await Repo.GetByConditionAsync(g => ids.Contains(g.Id) && g.DeletedAt == null);
            var members = await Repository<GroupMember>().GetByConditionAsync(m => ids.Contains(m.GroupId));
            var owners = groups.Select(g => g.OwnerId).Distinct().ToList();
            var names = (await Repository<User>().GetByConditionAsync(u => owners.Contains(u.Id))).ToDictionary(u => u.Id, u => u.FullName);
            return groups.Select(g => new GroupView(g.Id, g.Name, g.OwnerId, names.GetValueOrDefault(g.OwnerId, "?"),
                g.MaxMembers, members.Count(m => m.GroupId == g.Id), g.CreatedAt)).ToList();
        }

        public Task LeaveAsync(int groupId, int userId) => ChangeGroupAsync(groupId, userId, false, async g => {
            if (g.OwnerId == userId) throw new ConflictException("Chuyển chủ nhóm hoặc giải tán trước khi rời nhóm.");
            var member = (await Repository<GroupMember>().GetByConditionAsync(m => m.GroupId == groupId && m.UserId == userId)).SingleOrDefault()
                ?? throw new NotFoundException("GroupMember", userId);
            await Repository<GroupMember>().RemoveAsync(member);
        }, "group.leave");

        public Task RemoveMemberAsync(int groupId, int memberId, int ownerId) => ChangeGroupAsync(groupId, ownerId, true, async g => {
            if (memberId == g.OwnerId) throw new ConflictException("Không thể loại chủ nhóm.");
            var member = (await Repository<GroupMember>().GetByConditionAsync(m => m.GroupId == groupId && m.UserId == memberId)).SingleOrDefault()
                ?? throw new NotFoundException("GroupMember", memberId);
            await Repository<GroupMember>().RemoveAsync(member);
        }, "group.remove-member");

        public Task RenameAsync(int groupId, string name, int ownerId) => ChangeGroupAsync(groupId, ownerId, true, g => {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) throw new BadRequestException("Tên nhóm từ 1 đến 100 ký tự.");
            g.Name = name.Trim(); Repo.UpdateProperties(g, nameof(PlayGroup.Name)); return Task.CompletedTask;
        }, "group.rename");

        public Task DisbandAsync(int groupId, int ownerId) => ChangeGroupAsync(groupId, ownerId, true, async g => {
            if (await Repository<Booking>().CountByConditionAsync(b => b.GroupId == groupId && b.DeletedAt == null
                && (b.Status == Badminton.Core.Enums.BookingStatus.Pending || b.Status == Badminton.Core.Enums.BookingStatus.Confirmed)) > 0)
                throw new ConflictException("Nhóm còn đơn đang hoạt động. Xử lý đơn trước khi giải tán.");
            g.DeletedAt = DateTime.UtcNow; Repo.UpdateProperties(g, nameof(PlayGroup.DeletedAt));
        }, "group.disband");

        public Task TransferAsync(int groupId, int newOwnerId, int ownerId) => ChangeGroupAsync(groupId, ownerId, true, async g => {
            await RequireActiveAsync(newOwnerId);
            if (await Repository<GroupMember>().CountByConditionAsync(m => m.GroupId == groupId && m.UserId == newOwnerId) == 0)
                throw new ConflictException("Chủ nhóm mới phải là thành viên hiện tại.");
            g.OwnerId = newOwnerId; g.InviteTokenHash = Hash(NewInvite());
            Repo.UpdateProperties(g, nameof(PlayGroup.OwnerId), nameof(PlayGroup.InviteTokenHash));
        }, "group.transfer");

        public async Task<string> RotateInviteAsync(int groupId, int ownerId)
        {
            var code = NewInvite();
            await ChangeGroupAsync(groupId, ownerId, true, g => {
                g.InviteTokenHash = Hash(code); Repo.UpdateProperties(g, nameof(PlayGroup.InviteTokenHash)); return Task.CompletedTask;
            }, "group.rotate-invite");
            return code;
        }

        private Task ChangeGroupAsync(int id, int actorId, bool ownerOnly, Func<PlayGroup, Task> action, string eventName)
            => Uow.RunInTransactionAsync(async () => {
                await Uow.LockGroupAsync(id);
                await RequireActiveAsync(actorId);
                var g = await MustGetAsync(id);
                if (ownerOnly && g.OwnerId != actorId) throw new ConflictException("Chỉ chủ nhóm được thực hiện thao tác này.");
                var before = new { g.Name, g.OwnerId, g.DeletedAt };
                await action(g); await SaveAsync();
                await _audit.LogAsync(actorId, eventName, "PlayGroup", id.ToString(), before, new { g.Name, g.OwnerId, g.DeletedAt });
            });

        private static string NewInvite() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim())));

        private async Task RequireActiveAsync(int id)
        {
            var user = await Repository<User>().GetByIdAsync(id);
            if (user is null || user.DeletedAt.HasValue || user.Status != Badminton.Core.Enums.UserStatus.Active || !user.IsVerified)
                throw new ConflictException("Tài khoản không còn hoạt động.");
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
