using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Account;
using Badminton.Core.Base;
using Badminton.Core.Security;
using ModelViews.Account;
using ModelViews.Voucher;
using Services;

namespace Services
{
    public sealed class AccountService : BaseService, IAccountService
    {
        // Redeem rate: 1.000 điểm = 20.000đ (docs 01 §5). Mirrors PointService.
        private const int PointsPerVndUnit = 1_000;
        private const int VndPerPointsUnit = 20_000;

        private readonly Contract.Services.Membership.IMembershipService _membership;
        public AccountService(IUnitOfWork uow, ICurrentUser currentUser,
            Contract.Services.Membership.IMembershipService membership)
            : base(uow, currentUser) { _membership = membership; }

        public async Task<ProfileView> GetProfileAsync(int userId)
        {
            var u = await Repository<User>().GetByIdAsync(userId);
            if (u is null || u.DeletedAt.HasValue || u.Status != Badminton.Core.Enums.UserStatus.Active)
                throw new ConflictException("Tài khoản không hoạt động.");
            return new ProfileView(u.Id, u.FullName, u.PhoneNumber, u.Email, u.AvatarUrl,
                u.Tier.ToString(), u.MembershipExpiresAt, _membership.EffectiveMultiplier(u));
        }

        public async Task<ProfileView> UpdateProfileAsync(int userId, UpdateProfileRequest request)
        {
            await GetProfileAsync(userId);
            if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 100)
                throw new BadRequestException("Tên phải có từ 1 đến 100 ký tự.");
            var avatar = request.AvatarUrl?.Trim();
            if (!string.IsNullOrEmpty(avatar) && (!Uri.TryCreate(avatar, UriKind.Absolute, out var uri)
                || (uri.Scheme != "https" && uri.Scheme != "http") || avatar.Length > 500))
                throw new BadRequestException("Ảnh đại diện phải là URL HTTP/HTTPS hợp lệ, tối đa 500 ký tự.");
            var u = (await Repository<User>().GetByIdAsync(userId))!;
            u.FullName = request.FullName.Trim();
            u.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            u.AvatarUrl = string.IsNullOrEmpty(avatar) ? null : avatar;
            u.UpdatedAt = DateTime.UtcNow;
            Repository<User>().Update(u);
            await SaveAsync();
            return await GetProfileAsync(userId);
        }

        public async Task<PointSummary> GetPointsAsync(int userId)
        {
            var repo = Repository<PointLedger>();
            var balance = await repo.SumIntByConditionAsync(
                p => p.UserId == userId, p => p.Delta);
            var redeemableVnd = balance * VndPerPointsUnit / PointsPerVndUnit;
            return new PointSummary(balance, redeemableVnd, VndPerPointsUnit, PointsPerVndUnit);
        }

        public async Task<IReadOnlyList<MyVoucher>> GetMyVouchersAsync(int userId)
        {
            var redemptions = await Repository<VoucherRedemption>()
                .GetByConditionAsync(r => r.UserId == userId);
            var usedVoucherIds = redemptions
                .Select(r => r.VoucherId)
                .Distinct()
                .ToHashSet();

            var activeVouchers = await Repository<Voucher>().GetByConditionAsync(v => v.IsActive);
            return activeVouchers
                .Where(v => IsAvailable(v) && !usedVoucherIds.Contains(v.Id))
                .OrderBy(v => v.MinSpend)
                .Select(v => ToMyVoucher(v, alreadyUsed: false))
                .ToList();
        }

        public async Task<AccountSummary> GetAccountSummaryAsync(int userId)
        {
            var points = await GetPointsAsync(userId);

            var redemptions = await Repository<VoucherRedemption>()
                .GetByConditionAsync(r => r.UserId == userId);
            var usedIds = redemptions.Select(r => r.VoucherId).Distinct().ToHashSet();
            var voucherUsed = usedIds.Count > 0;

            MyVoucher? usedVoucher = null;
            if (voucherUsed)
            {
                var mostRecent = redemptions
                    .Where(r => usedIds.Contains(r.VoucherId))
                    .OrderByDescending(r => r.RedeemedAt)
                    .FirstOrDefault();
                if (mostRecent is not null)
                {
                    var v = await Repository<Voucher>().GetByIdAsync(mostRecent.VoucherId);
                    if (v is not null)
                        usedVoucher = ToMyVoucher(v, alreadyUsed: true);
                }
            }

            var activeVouchers = await Repository<Voucher>().GetByConditionAsync(x => x.IsActive);

            var availableVouchers = activeVouchers
                .Where(v => IsAvailable(v) && !usedIds.Contains(v.Id))
                .OrderBy(v => v.MinSpend)
                .Select(v => ToMyVoucher(v, alreadyUsed: false))
                .ToList();
            var nextVoucher = availableVouchers.FirstOrDefault();

            return new AccountSummary(
                points.Balance,
                points.RedeemableVnd,
                points.VndPer1000Points,
                points.PointsPerVndUnit,
                voucherUsed,
                usedVoucher,
                nextVoucher,
                availableVouchers);
        }

        private static bool IsAvailable(Voucher v) => v.IsActive && v.ValidFrom <= Badminton.Core.Utils.HanoiTime.HanoiToday()
            && (!v.ValidUntil.HasValue || v.ValidUntil.Value >= Badminton.Core.Utils.HanoiTime.HanoiToday()) && v.UsedQty < v.TotalQty;

        private static MyVoucher ToMyVoucher(Voucher v, bool alreadyUsed) => new(
            v.Id, v.Code, v.Type.ToString(), v.Value, v.MaxValue, v.MinSpend,
            alreadyUsed,
            v.ValidFrom.ToString(),
            v.ValidUntil?.ToString(),
            v.IsActive);
    }
}
