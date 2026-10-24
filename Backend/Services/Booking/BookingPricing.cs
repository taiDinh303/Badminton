using Badminton.Core.Base;
using Contract.Repositories.Entity;
using Contract.Services.Pricing;

namespace Services;

public static class BookingPricing
{
    public static int SlotTotal(Court court, TimeSlot slot, DateOnly date, int players, IPricingService pricing)
    {
        if (players < 1 || players > court.Capacity)
            throw new BadRequestException($"Số người phải từ 1 đến {court.Capacity} (sức chứa sân).");
        return checked((int)pricing.WithWeekend(court.PriceLowPeak, court.PricePeak, slot.IsPeak,
            court.WeekendSurcharge, date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday));
    }

    public static (int Points, int Discount) Redeem(int requested, int availableAmount)
    {
        if (requested < 0) throw new BadRequestException("Số điểm không được âm.");
        var points = Math.Min(requested, (int)(Math.Max(0, (long)availableAmount) * 1000 / 20000));
        if (points < 1000) points = 0;
        return (points, checked((int)((long)points * 20000 / 1000)));
    }
}
