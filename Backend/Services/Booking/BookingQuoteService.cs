using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Points;
using Contract.Services.Pricing;
using Contract.Services.Voucher;
using ModelViews.Booking;

namespace Services;

public sealed class BookingQuoteService(IUnitOfWork uow, IPricingService pricing, IVoucherService vouchers, IPointService points)
{
    public async Task<BookingPriceBreakdown> QuoteAsync(CreateBookingRequest request, int userId)
    {
        var user = await uow.GetRepository<User>().GetByIdAsync(userId);
        if (user is null || user.DeletedAt.HasValue || user.Status != UserStatus.Active)
            throw new ConflictException("Tài khoản không còn hoạt động.");
        var court = await uow.GetRepository<Court>().GetByIdAsync(request.CourtId);
        var slot = await uow.GetRepository<TimeSlot>().GetByIdAsync(request.SlotId);
        if (court is null || !court.IsActive || court.DeletedAt.HasValue || slot is null || !slot.IsActive || slot.DeletedAt.HasValue)
            throw new BadRequestException("Sân hoặc khung giờ không hoạt động.");
        if (!DateOnly.TryParse(request.Date, out var date)) throw new BadRequestException("Ngày không hợp lệ.");
        var total = BookingPricing.SlotTotal(court, slot, date, request.PlayerCount, pricing);
        var discount = string.IsNullOrWhiteSpace(request.VoucherCode) ? 0
            : await vouchers.PreviewDiscountAsync(request.VoucherCode.Trim(), userId, total);
        var (redeemed, pointsDiscount) = BookingPricing.Redeem(request.PointsToRedeem ?? 0, total - discount);
        if (redeemed > await points.GetBalanceAsync(userId)) throw new ConflictException("Không đủ điểm để đổi.");
        return new BookingPriceBreakdown(total, 0, discount, pointsDiscount, total,
            Math.Max(0, total - discount - pointsDiscount));
    }
}
