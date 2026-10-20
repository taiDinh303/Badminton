using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Badminton.Core.Enums;
using Badminton.Core.Store;

namespace Contract.Services.Membership
{
    public interface IMembershipService
    {
        decimal EffectiveMultiplier(User user);
        Task<decimal> EffectiveMultiplierAsync(int userId);
        Task<User> AssignTierAsync(int userId, MembershipTier tier, int actorId);
        Task<IReadOnlyList<MembershipView>> GetMembersAsync();
    }

    public sealed record MembershipView(
        int Id, string FullName, string PhoneNumber, MembershipTier Tier,
        DateTime? ExpiresAt, bool Lapsed, decimal Multiplier);
}
