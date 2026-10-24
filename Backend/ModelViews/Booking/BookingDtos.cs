namespace ModelViews.Booking
{
    public record CreateBookingRequest(int CourtId, string Date, int SlotId,
        int PlayerCount = 1, int? GroupId = null, string? VoucherCode = null,
        string? IdempotencyKey = null, string? Note = null, int? PointsToRedeem = null);

    public record BookingPriceBreakdown(int BasePrice, int WeekendSurcharge,
        int VoucherDiscount, int PointsDiscount, int Total, int Payable);

    public record BookingView(int Id, string BookingCode, int CourtId, string CourtName,
        string Date, string SlotLabel, int PlayerCount, string Status,
        int TotalAmount, int DiscountAmount, int PayableAmount, int PointsEarned,
        int PointsRedeemed, int? GroupId, int? VoucherRedemptionId, DateTime CreatedAt,
        string? UserName, BookingPriceBreakdown Price, string? CancelReason,
        string? QrToken = null, DateTime? CheckedInAt = null, int UserId = 0);

    public record CourtAvailabilityView(int CourtId, string CourtCode, string CourtName,
        int PriceLowPeak, int PricePeak, List<SlotView> Slots, int Capacity = 10);

    public record SlotView(int SlotId, string Label, string StartTime, string EndTime,
        bool IsPeak, bool IsLocked, string LockReason);
}
