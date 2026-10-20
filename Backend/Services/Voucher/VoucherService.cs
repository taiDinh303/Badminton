using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Voucher;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Badminton.Core.Security;
using ModelViews.Voucher;

namespace Services
{
    public sealed class VoucherService : BaseService, IVoucherService
    {
        private readonly IAuditService _audit;
        public VoucherService(IUnitOfWork uow, ICurrentUser currentUser, IAuditService audit)
            : base(uow, currentUser) { _audit = audit; }

        private IGenericRepository<Voucher> Repo => Repository<Voucher>();

        public async Task<IReadOnlyList<VoucherView>> GetAllAsync(bool activeOnly = true)
        {
            var all = activeOnly
                ? await Repo.GetByConditionAsync(v => v.IsActive)
                : await Repo.GetAllAsync();
            return all.Select(ToView).ToList();
        }

        public async Task<VoucherView> GetByCodeAsync(string code)
        {
            var found = (await Repo.GetByConditionAsync(v => v.Code == code)).FirstOrDefault();
            if (found is null) throw new NotFoundException("Voucher", code);
            return ToView(found);
        }

        public async Task<int> CreateAsync(CreateVoucherRequest request, int actorId)
        {
            var dupes = await Repo.GetByConditionAsync(v => v.Code == request.Code);
            if (dupes.Count > 0) throw new ConflictException($"Mã {request.Code} đã tồn tại.");

            var entity = new Voucher
            {
                Code = request.Code,
                Type = ParseType(request.Type),
                Value = request.Value,
                MaxValue = request.MaxValue,
                MinSpend = request.MinSpend,
                MaxPerUser = request.MaxPerUser,
                TotalQty = request.TotalQty,
                ValidFrom = string.IsNullOrWhiteSpace(request.ValidFrom)
                    ? HanoiTime.HanoiToday()
                    : ParseDate(request.ValidFrom),
                ValidUntil = string.IsNullOrWhiteSpace(request.ValidUntil)
                    ? null
                    : ParseDate(request.ValidUntil)
            };
            _ = Repo.AddAsync(entity);
            await SaveAsync();
            await _audit.LogAsync(actorId, "voucher.create", "Voucher", entity.Id.ToString(),
                null, entity, $"Tạo voucher {entity.Code}");
            return entity.Id;
        }

        public async Task DeactivateAsync(int id, int actorId)
        {
            var e = await MustGetAsync(id);
            e.IsActive = false;
            e.UpdatedAt = DateTime.UtcNow;
            Repo.Update(e);
            await SaveAsync();
            await _audit.LogAsync(actorId, "voucher.deactivate", "Voucher", id.ToString(),
                e, e, "Vô hiệu voucher");
        }

        public async Task<int> PreviewDiscountAsync(string code, int userId, int baseAmount)
        {
            var v = (await Repo.GetByConditionAsync(x => x.Code == code)).FirstOrDefault();
            if (v is null || !v.IsActive) return 0;

            // P1-4: Hanoi business day, not the machine clock.
            var today = HanoiTime.HanoiToday();
            if (v.ValidUntil.HasValue && today > v.ValidUntil.Value) return 0;
            if (today < v.ValidFrom) return 0;
            if (baseAmount < v.MinSpend) return 0;

            var redemptions = Repository<VoucherRedemption>();
            var used = await redemptions.GetByConditionAsync(
                r => r.VoucherId == v.Id && r.UserId == userId);
            if (used.Count >= v.MaxPerUser) return 0;

            return ComputeDiscount(v, baseAmount);
        }

        // ── Atomic reservation (total-qty + per-user limits) ──────────────

        public async Task<(int Discount, VoucherRedemption Redemption)> ReserveVoucherAsync(
            string code, int userId, int baseAmount)
        {
            var v = (await Repo.GetByConditionAsync(x => x.Code == code)).FirstOrDefault();
            if (v is null || !v.IsActive)
                throw new BadRequestException("Voucher khong ap dung cho don nay.");

            // P1-4: Hanoi business day, not the machine clock.
            var today = HanoiTime.HanoiToday();
            if (v.ValidUntil.HasValue && today > v.ValidUntil.Value)
                throw new BadRequestException("Voucher da het han.");
            if (today < v.ValidFrom)
                throw new BadRequestException("Voucher chua co hieu luc.");
            if (baseAmount < v.MinSpend)
                throw new BadRequestException("So tien don thap hon muc toi thieu cua voucher.");

            var redemptions = Repository<VoucherRedemption>();
            var prior = await redemptions.GetByConditionAsync(
                r => r.VoucherId == v.Id && r.UserId == userId);
            if (prior.Count >= v.MaxPerUser)
                throw new ConflictException("Da dung het so luot voucher nay (MaxPerUser).");

            var discount = ComputeDiscount(v, baseAmount);

            int claimed = await Uow.ExecuteSqlRawAsync(
                "UPDATE Vouchers SET UsedQty = UsedQty + 1 " +
                "WHERE Code = @p0 AND IsActive = 1 AND UsedQty < TotalQty",
                code);
            if (claimed == 0)
                throw new ConflictException("Voucher da het luot (total cap).");

            var redemption = new VoucherRedemption
            {
                VoucherId = v.Id,
                UserId = userId,
                DiscountAmount = discount
            };
            _ = redemptions.AddAsync(redemption); // staged, not committed

            return (discount, redemption);
        }

        public async Task ReleaseVoucherAsync(int redemptionId)
        {
            var redemption = await Repository<VoucherRedemption>().GetByIdAsync(redemptionId);
            if (redemption is null) return;

            var v = await Repo.GetByIdAsync(redemption.VoucherId);
            if (v is not null)
            {
                // Decrement only if the counter is still above zero (guarded).
                var released = await Uow.ExecuteSqlRawAsync(
                    "UPDATE Vouchers SET UsedQty = UsedQty - 1 " +
                    "WHERE Id = @p0 AND UsedQty > 0",
                    v.Id);
                if (released > 0)
                    _ = Repository<VoucherRedemption>().RemoveAsync(redemption);
            }
            else
            {
                _ = Repository<VoucherRedemption>().RemoveAsync(redemption);
            }
            await SaveAsync();
        }

        private static int ComputeDiscount(Voucher v, int baseAmount)
        {
            if (v.Type == VoucherType.Percent)
            {
                var discount = (int)Math.Round((decimal)baseAmount * (v.Value / 100m));
                if (v.MaxValue.HasValue && discount > v.MaxValue.Value)
                    discount = v.MaxValue.Value;
                return discount;
            }
            return v.Value;
        }

        private async Task<Voucher> MustGetAsync(int id)
        {
            var e = await Repo.GetByIdAsync(id);
            if (e is null || e.DeletedAt.HasValue) throw new NotFoundException("Voucher", id);
            return e;
        }

        private static VoucherType ParseType(string s) =>
            s.Equals("percent", System.StringComparison.OrdinalIgnoreCase)
                ? VoucherType.Percent : VoucherType.Fixed;

        // P1-4: an unparseable date falls back to the Hanoi business day.
        private static DateOnly ParseDate(string s) =>
            DateOnly.TryParse(s, out var d) ? d : HanoiTime.HanoiToday();

        private static VoucherView ToView(Voucher v) => new(
            v.Id, v.Code, v.Type.ToString(), v.Value, v.MaxValue, v.MinSpend,
            v.MaxPerUser, v.TotalQty, v.UsedQty,
            v.ValidUntil?.ToString(), v.ValidFrom.ToString(), v.IsActive);
    }
}
