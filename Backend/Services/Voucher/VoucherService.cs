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
            return all.Where(v => !activeOnly || !IsExpired(v)).Select(ToView).ToList();
        }

        public async Task<VoucherView> GetByCodeAsync(string code)
        {
            code = code.Trim().ToUpperInvariant();
            var found = (await Repo.GetByConditionAsync(v => v.Code.ToUpper() == code)).FirstOrDefault();
            if (found is null) throw new NotFoundException("Voucher", code);
            return ToView(found);
        }

        public async Task<int> CreateAsync(CreateVoucherRequest request, int actorId)
        {
            var type = ParseType(request.Type);
            var from = string.IsNullOrWhiteSpace(request.ValidFrom) ? HanoiTime.HanoiToday() : ParseDate(request.ValidFrom);
            DateOnly? until = string.IsNullOrWhiteSpace(request.ValidUntil) ? null : ParseDate(request.ValidUntil);
            if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 30 || request.Value <= 0
                || (type == VoucherType.Percent && request.Value > 100) || request.MinSpend < 0
                || request.MaxValue is <= 0 || request.TotalQty <= 0 || (until.HasValue && until < from))
                throw new BadRequestException("Mã, giá trị, số lượng hoặc thời hạn voucher không hợp lệ.");
            request = request with { Code = request.Code.Trim().ToUpperInvariant() };
            var dupes = await Repo.GetByConditionAsync(v => v.Code.ToUpper() == request.Code);
            if (dupes.Count > 0) throw new ConflictException($"Mã {request.Code} đã tồn tại.");

            var entity = new Voucher
            {
                Code = request.Code,
                Type = type,
                Value = request.Value,
                MaxValue = request.MaxValue,
                MinSpend = request.MinSpend,
                MaxPerUser = 1,
                TotalQty = request.TotalQty,
                ValidFrom = from,
                ValidUntil = until
            };
            await Repo.AddAsync(entity);
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
            code = code.Trim().ToUpperInvariant();
            var v = (await Repo.GetByConditionAsync(x => x.Code.ToUpper() == code)).FirstOrDefault();
            ValidateApplicable(v, baseAmount);

            var redemptions = Repository<VoucherRedemption>();
            var used = await redemptions.GetByConditionAsync(
                r => r.VoucherId == v!.Id && r.UserId == userId);
            if (used.Count > 0)
                throw new ConflictException("Bạn đã sử dụng mã voucher này. Hãy chọn mã khác.");
            if (v!.UsedQty >= v.TotalQty) throw new ConflictException("Voucher đã hết lượt.");

            return ComputeDiscount(v!, baseAmount);
        }

        // ── Atomic reservation (total-qty + per-user limits) ──────────────

        public async Task<(int Discount, VoucherRedemption Redemption)> ReserveVoucherAsync(
            string code, int userId, int baseAmount)
        {
            await Uow.LockVoucherUserAsync(userId);
            code = code.Trim().ToUpperInvariant();
            var v = (await Repo.GetByConditionAsync(x => x.Code.ToUpper() == code)).FirstOrDefault();
            ValidateApplicable(v, baseAmount);

            var redemptions = Repository<VoucherRedemption>();
            var prior = await redemptions.GetByConditionAsync(
                r => r.VoucherId == v!.Id && r.UserId == userId);
            if (prior.Count > 0)
                throw new ConflictException("Bạn đã sử dụng mã voucher này. Hãy chọn mã khác.");

            var discount = ComputeDiscount(v!, baseAmount);

            int claimed = await Uow.Vouchers.ReserveAsync(v!.Code);
            if (claimed == 0)
                throw new ConflictException("Voucher da het luot (total cap).");

            var redemption = new VoucherRedemption
            {
                VoucherId = v!.Id,
                UserId = userId,
                DiscountAmount = discount
            };
            await redemptions.AddAsync(redemption); // staged, not committed

            return (discount, redemption);
        }

        public Task ReleaseVoucherAsync(int redemptionId) => Uow.RunInTransactionAsync(async () =>
        {
            var repo = Repository<VoucherRedemption>();
            var candidate = (await repo.GetByConditionAsync(r => r.Id == redemptionId)).SingleOrDefault();
            if (candidate is null) return;
            await Uow.LockPointsUserAsync(candidate.UserId);
            await Uow.LockVoucherUserAsync(candidate.UserId);
            var redemption = (await repo.GetByConditionAsync(r => r.Id == redemptionId)).SingleOrDefault();
            if (redemption is null) return;
            if (await Uow.Vouchers.DeleteRedemptionAsync(redemptionId) > 0)
                await Uow.Vouchers.ReleaseAsync(redemption.VoucherId);
            await SaveAsync();
        });

        private static void ValidateApplicable(Voucher? voucher, int baseAmount)
        {
            if (baseAmount < 0) throw new BadRequestException("Số tiền không được âm.");
            if (voucher is null || !voucher.IsActive || voucher.DeletedAt.HasValue)
                throw new BadRequestException("Voucher không áp dụng cho đơn này.");
            var today = HanoiTime.HanoiToday();
            if (voucher.ValidUntil is { } end && end < today) throw new BadRequestException("Voucher đã hết hạn sử dụng.");
            if (today < voucher.ValidFrom) throw new BadRequestException("Voucher chưa có hiệu lực.");
            if (baseAmount < voucher.MinSpend) throw new BadRequestException("Số tiền đơn thấp hơn mức tối thiểu của voucher.");
            if (voucher.Value <= 0 || !Enum.IsDefined(voucher.Type) || voucher.MinSpend < 0 || voucher.TotalQty <= 0
                || voucher.MaxValue is <= 0 || (voucher.Type == VoucherType.Percent && voucher.Value > 100))
                throw new BadRequestException("Voucher có cấu hình không hợp lệ.");
        }

        private static int ComputeDiscount(Voucher v, int baseAmount)
        {
            decimal discount = v.Type == VoucherType.Percent ? Math.Round((decimal)baseAmount * v.Value / 100m) : v.Value;
            if (v.MaxValue is { } cap) discount = Math.Min(discount, cap);
            return (int)Math.Clamp(discount, 0, baseAmount);
        }

        private async Task<Voucher> MustGetAsync(int id)
        {
            var e = await Repo.GetByIdAsync(id);
            if (e is null || e.DeletedAt.HasValue) throw new NotFoundException("Voucher", id);
            return e;
        }

        private static VoucherType ParseType(string s) => s?.Trim().ToLowerInvariant() switch
        {
            "percent" => VoucherType.Percent,
            "fixed" => VoucherType.Fixed,
            _ => throw new BadRequestException("Loại voucher không hợp lệ.")
        };

        private static DateOnly ParseDate(string s) =>
            DateOnly.TryParse(s, out var d) ? d : throw new BadRequestException("Ngày voucher không hợp lệ.");

        private static VoucherView ToView(Voucher v) => new(
            v.Id, v.Code, v.Type.ToString(), v.Value, v.MaxValue, v.MinSpend,
            v.MaxPerUser, v.TotalQty, v.UsedQty,
            v.ValidFrom.ToString("yyyy-MM-dd"), v.ValidUntil?.ToString("yyyy-MM-dd"), v.IsActive && !IsExpired(v));

        private static bool IsExpired(Voucher v) => v.ValidUntil is { } end && end < HanoiTime.HanoiToday();
    }
}
