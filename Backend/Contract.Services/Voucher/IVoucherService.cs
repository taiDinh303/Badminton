using ModelViews.Voucher;
using Contract.Repositories.Entity;

namespace Contract.Services.Voucher
{
    public interface IVoucherService
    {
        Task<IReadOnlyList<VoucherView>> GetAllAsync(bool activeOnly = true);
        Task<VoucherView> GetByCodeAsync(string code);
        Task<int> CreateAsync(CreateVoucherRequest request, int actorId);
        Task DeactivateAsync(int id, int actorId);
        Task<int> PreviewDiscountAsync(string code, int userId, int baseAmount);

        /// <summary>
        /// Atomically reserve a voucher for an order that the caller is about
        /// to persist. Enforces the hard limits:
        ///   • <see cref="Voucher.TotalQty"/> — the <c>UsedQty</c> counter is
        ///     bumped by a single guarded UPDATE (<c>UsedQty &lt; TotalQty</c>);
        ///     a 0-row result means the voucher is exhausted ("hết lượt") and
        ///     a Conflict is raised so the order rolls back with it.
        ///   • <see cref="Voucher.MaxPerUser"/> — counted in-transaction; the
        ///     unique (VoucherId, UserId) redemption index is the database
        ///     backstop when MaxPerUser == 1.
        /// Stages a <see cref="VoucherRedemption"/> (un-saved so the caller's
        /// transaction owns the single commit) and returns the discount plus
        /// that row, which the caller links to the order.
        /// </summary>
        Task<(int Discount, VoucherRedemption Redemption)> ReserveVoucherAsync(
            string code, int userId, int baseAmount);

        /// <summary>
        /// Return a previously reserved voucher (booking cancelled before
        /// payment): decrement <c>UsedQty</c> and drop the redemption so
        /// another customer can still use it.
        /// </summary>
        Task ReleaseVoucherAsync(int redemptionId);
    }
}
