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
        Task<(int Discount, VoucherRedemption Redemption)> ReserveVoucherAsync(
            string code, int userId, int baseAmount);
        Task ReleaseVoucherAsync(int redemptionId);
    }
}
