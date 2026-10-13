using M.Core.Enums;
using ModelViews.TimeSlot;

namespace ModelViews.Booking
{
    // ---------- Slot selection inside a booking ----------
    public record SlotSelection(int CourtId, int TimeSlotId);

    // ---------- Booking ----------
    public record CreateBookingRequest(DateOnly BookingDate, List<SlotSelection> Slots,
        string? Note);

    public record BookingView(int Id, string BookingCode, DateOnly BookingDate,
        decimal TotalAmount, BookingStatus Status, string? Note, string? AppliedFeePolicy,
        DateTime CreatedAt, string? UserName,
        List<BookingDetailView> Details);

    public record BookingDetailView(int CourtId, string CourtName, int TimeSlotId,
        TimeOnly StartTime, TimeOnly EndTime, decimal Price);

    // ---------- Free slots for a court type / day ----------
    public record CourtSlotAvailabilityView(int CourtId, string CourtCode, string CourtName,
        decimal PricePerHour, string CourtTypeName, List<TimeSlotView> FreeSlots);
}
