using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Voucher;
using Badminton.Core.Base;
using Badminton.Core.Enums;
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
            if (baseAmount < v.MinSpend) return 0;

            var redemptions = Repository<VoucherRedemption>();
            var used = await redemptions.GetByConditionAsync(
                r => r.VoucherId == v.Id && r.UserId == userId);
            if (used.Count >= v.MaxPerUser) return 0;

            return ComputeDiscount(v, baseAmount);
        }

        // ── Atomic reservation (total-qty + per-user limits) ──────────────

        /// <summary>
        /// Reserve a voucher inside the caller's ambient transaction.
        /// Stages (does NOT commit) so the caller's transaction owns the
        /// single save:
        ///   1. validate validity / min-spend / per-user cap;
        ///   2. atomically bump <c>Voucher.UsedQty</c> guarded by
        ///      <c>UsedQty &lt; TotalQty</c> — a 0-row result means the
        ///      voucher is exhausted ("hết lượt") and the booking rolls back;
        ///   3. stage a <see cref="VoucherRedemption"/> (the unique
        ///      (VoucherId,UserId) index is the database backstop for
        ///      MaxPerUser == 1) and return it so the caller links it to the
        ///      order before the commit.
        /// </summary>
        public async Task<(int Discount, VoucherRedemption Redemption)> ReserveVoucherAsync(
            string code, int userId, int baseAmount)
        {
            var v = (await Repo.GetByConditionAsync(x => x.Code == code)).FirstOrDefault();
            if (v is null || !v.IsActive)
                throw new BadRequestException("Voucher khong ap dung cho don nay.");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (v.ValidUntil.HasValue && today > v.ValidUntil.Value)
                throw new BadRequestException("Voucher da het han.");
            if (today < v.ValidFrom)
                throw new BadRequestException("Voucher chua co hieu luc.");
            if (baseAmount < v.MinSpend)
                throw new BadRequestException("So tien don thap hon muc toi thieu cua voucher.");

            // Per-user cap: enforce in-transaction. The (VoucherId,UserId)
            // unique index is the hard backstop for MaxPerUser == 1; for
            // larger caps the count is authoritative within this transaction.
            var redemptions = Repository<VoucherRedemption>();
            var prior = await redemptions.GetByConditionAsync(
                r => r.VoucherId == v.Id && r.UserId == userId);
            if (prior.Count >= v.MaxPerUser)
                throw new ConflictException("Da dung het so luot voucher nay (MaxPerUser).");

            var discount = ComputeDiscount(v, baseAmount);

            // ATOMIC total-quantity guard: increment only while under the cap.
            // A 0-row result means the voucher is exhausted — the whole
            // booking transaction rolls back with it.
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

        /// <summary>
        /// Release a reserved voucher when the booking is cancelled before
        /// payment: decrement <c>UsedQty</c> and drop the redemption so
        /// another customer can still use it.
        /// </summary>
        public async Task ReleaseVoucherAsync(int redemptionId)
        {
            // Use the *tracked* lookup so that when the caller (e.g. BookingService
            // .CancelAsync) has already loaded this redemption in the same scoped
            // DbContext, we get the same instance rather than attaching a
            // duplicate with the same key (which EF would reject).
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

        private static DateOnly ParseDate(string s) =>
            DateOnly.TryParse(s, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);

        private static VoucherView ToView(Voucher v) => new(
            v.Id, v.Code, v.Type.ToString(), v.Value, v.MaxValue, v.MinSpend,
            v.MaxPerUser, v.TotalQty, v.UsedQty,
            v.ValidUntil?.ToString(), v.ValidFrom.ToString(), v.IsActive);
    }
}
