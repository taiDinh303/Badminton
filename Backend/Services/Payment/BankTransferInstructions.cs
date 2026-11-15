using Badminton.Core.Base;
using Badminton.Core.Store;
using Contract.Repositories.Entity;
using ModelViews.Payment;
using System.Globalization;

namespace Services;

public static class BankTransferInstructions
{
    public static BankTransferView? Create(BankTransferSettings settings, Booking booking)
    {
        // No fallback across branches: never direct money to another branch's account.
        if (!settings.Branches.TryGetValue(booking.BranchId.ToString(CultureInfo.InvariantCulture), out var account))
            return null; // Preserve the existing QR-at-counter contract when not configured.
        if (!account.IsValid()) throw new ConflictException("BANK_TRANSFER_UNAVAILABLE: Cấu hình tài khoản cơ sở chưa hợp lệ.");
        if (booking.PayableAmount <= 0 || booking.PendingExpiresAt is null)
            throw new ConflictException("Đơn không cần chuyển khoản hoặc không có hạn thanh toán.");
        var content = "BM" + booking.Id.ToString(CultureInfo.InvariantCulture);
        var url = $"https://img.vietqr.io/image/{account.BankBin}-{account.AccountNumber}-compact2.png"
            + $"?amount={booking.PayableAmount.ToString(CultureInfo.InvariantCulture)}&addInfo={Uri.EscapeDataString(content)}&accountName={Uri.EscapeDataString(account.AccountName)}";
        return new(account.BankBin, account.AccountNumber, account.AccountName, booking.PayableAmount,
            "VND", content, url, DateTime.SpecifyKind(booking.PendingExpiresAt.Value, DateTimeKind.Utc), true);
    }
}
