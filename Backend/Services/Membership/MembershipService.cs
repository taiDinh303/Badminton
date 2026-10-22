using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Membership;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Store;
using Microsoft.Extensions.Options;

namespace Services
{
    public sealed class MembershipService : BaseService, IMembershipService
    {
        private readonly IOptions<MembershipSettings> _s;

        public MembershipService(IUnitOfWork uow,
            Badminton.Core.Security.ICurrentUser currentUser,
            IOptions<MembershipSettings> settings)
            : base(uow, currentUser)
        {
            _s = settings;
        }

        public decimal EffectiveMultiplier(User u)
        {
            var now = DateTime.UtcNow;
            if (u.Tier is MembershipTier.Silver or MembershipTier.Gold
                && u.MembershipExpiresAt is { } exp
                && exp > now)
            {
                return u.Tier == MembershipTier.Gold
                    ? _s.Value.MultiplierGold
                    : _s.Value.MultiplierSilver;
            }
            return 1.0m; // None, or a lapsed paid tier.
        }

        public async Task<decimal> EffectiveMultiplierAsync(int userId)
        {
            var u = await Repository<User>().GetByIdAsync(userId);
            return u is null ? 1.0m : EffectiveMultiplier(u);
        }

        public async Task<User> AssignTierAsync(int userId, MembershipTier tier,
            int actorId)
        {
            var u = await Repository<User>().GetByIdAsync(userId);
            if (u is null) throw new NotFoundException("User", userId);
            if (!Enum.IsDefined(tier)) throw new BadRequestException("Hạng thành viên không hợp lệ.");
            if (u.Status != UserStatus.Active || u.DeletedAt.HasValue) throw new ConflictException("Khách đã bị khóa hoặc xóa.");
            if (u.Role != Role.Customer)
                throw new ConflictException("Chỉ khách (Customer) mới được gán membership.");

            u.Tier = tier;
            u.MembershipExpiresAt = tier switch
            {
                MembershipTier.Silver
                    => DateTime.UtcNow.AddDays(_s.Value.SilverDays),
                MembershipTier.Gold
                    => DateTime.UtcNow.AddDays(_s.Value.GoldDays),
                _ => null
            };
            u.UpdatedAt = DateTime.UtcNow;
            Repository<User>().Update(u);
            await SaveAsync();
            return u;
        }

        public async Task<IReadOnlyList<MembershipView>> GetMembersAsync(bool includeCustomers = false)
        {
            var now = DateTime.UtcNow;
            var rows = await Repository<User>().GetByConditionAsync(
                u => (includeCustomers || u.Tier != MembershipTier.None) && u.Role == Role.Customer && u.DeletedAt == null && u.Status == UserStatus.Active);
            return rows
                .Select(u => new MembershipView(
                    u.Id, u.FullName, u.PhoneNumber, u.Tier, u.MembershipExpiresAt,
                    u.MembershipExpiresAt is { } e && e <= now,
                    EffectiveMultiplier(u)))
                .ToList();
        }
    }
}
