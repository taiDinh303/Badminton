namespace ModelViews.Account
{
    public sealed record PointSummary(
        int Balance,          // total accumulated points
        int RedeemableVnd,   // how much VND those points can currently offset
        int VndPer1000Points, // 20.000đ
        int PointsPerVndUnit); // 1.000 điểm

    public sealed record MyVoucher(int Id, string Code, string Type, int Value, int? MaxValue,
        int MinSpend, bool AlreadyUsed, string? ValidFrom, string? ValidUntil, bool IsActive);

    public sealed record AccountSummary(
        int PointsBalance,           // total accumulated points
        int PointsRedeemableVnd,    // how much VND those points can offset now
        int VndPer1000Points,       // 20.000đ
        int PointsPerVndUnit,       // 1.000 điểm
        bool VoucherUsed,
        MyVoucher? UsedVoucher,
        MyVoucher? NextVoucher,
        IReadOnlyList<MyVoucher> AvailableVouchers);
}

namespace ModelViews.Account
{
    public sealed record ProfileView(int Id, string FullName, string PhoneNumber, string? Email,
        string? AvatarUrl, string Tier, DateTime? MembershipExpiresAt, decimal Multiplier);
    public sealed record UpdateProfileRequest(
        [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)] string FullName,
        [System.ComponentModel.DataAnnotations.EmailAddress, System.ComponentModel.DataAnnotations.StringLength(150)] string? Email,
        [System.ComponentModel.DataAnnotations.StringLength(500)] string? AvatarUrl);
}
