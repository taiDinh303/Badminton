using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Voucher;
using M.Core.Base;
using M.Core.Enums;
using Microsoft.AspNetCore.Http;
using ModelViews.Voucher;

namespace Services
{
    public sealed class VoucherService : BaseService, IVoucherService
    {
        private readonly IAuditService _audit;
        public VoucherService(IUnitOfWork uow, IHttpContextAccessor http, IAuditService audit)
            : base(uow, http) { _audit = audit; }

        private IGenericRepository<Voucher> Repo => Repository<Voucher>();

        public async Task<IReadOnlyList<VoucherView>> GetAllAsync(bool activeOnly = true)
        {
            var all = await Repo.GetAllAsync();
            return all
                .Where(v => !activeOnly || v.IsActive)
                .Select(ToView)
                .ToList();
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
                    ? DateOnly.FromDateTime(DateTime.UtcNow)
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

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (v.ValidUntil.HasValue && today > v.ValidUntil.Value) return 0;
            if (today < v.ValidFrom) return 0;

            // min_spend on the pre-discount price
            if (baseAmount < v.MinSpend) return 0;

            // One redemption per customer
            var redemptions = Repository<VoucherRedemption>();
            var used = await redemptions.GetByConditionAsync(
                r => r.VoucherId == v.Id && r.UserId == userId);
            if (used.Count >= v.MaxPerUser) return 0;

            // percent (with cap) or fixed
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

        private static DateOnly ParseDate(string s) =>
            DateOnly.TryParse(s, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);

        private static VoucherView ToView(Voucher v) => new(
            v.Id, v.Code, v.Type.ToString(), v.Value, v.MaxValue, v.MinSpend,
            v.MaxPerUser, v.TotalQty, v.UsedQty,
            v.ValidUntil?.ToString(), v.ValidFrom.ToString(), v.IsActive);
    }
}
