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
            Microsoft.AspNetCore.Http.IHttpContextAccessor http,
            IOptions<MembershipSettings> settings)
            : base(uow, http)
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

        public async Task<IReadOnlyList<MembershipView>> GetMembersAsync()
        {
            var now = DateTime.UtcNow;
            var rows = await Repository<User>().GetByConditionAsync(
                u => u.Tier != MembershipTier.None && u.Role == Role.Customer);
            return rows
                .Select(u => new MembershipView(
                    u.Id, u.FullName, u.PhoneNumber, u.Tier, u.MembershipExpiresAt,
                    u.MembershipExpiresAt is { } e && e <= now,
                    EffectiveMultiplier(u)))
                .ToList();
        }
    }
}
