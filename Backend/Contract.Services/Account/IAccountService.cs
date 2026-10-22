using ModelViews.Account;
using ModelViews.Booking;
using ModelViews.Voucher;

namespace Contract.Services.Account
{
    public interface IAccountService
    {
        Task<ProfileView> GetProfileAsync(int userId);
        Task<ProfileView> UpdateProfileAsync(int userId, UpdateProfileRequest request);
        Task<PointSummary> GetPointsAsync(int userId);
        Task<IReadOnlyList<MyVoucher>> GetMyVouchersAsync(int userId);
        Task<AccountSummary> GetAccountSummaryAsync(int userId);
    }
}
