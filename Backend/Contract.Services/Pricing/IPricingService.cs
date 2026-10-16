using Badminton.Core.Enums;

namespace Contract.Services.Pricing
{
    public interface IPricingService
    {
        decimal BasePrice(int priceLowPeak, int pricePeak, bool isPeak);

        decimal WithWeekend(int priceLowPeak, int pricePeak, bool isPeak,
            decimal weekendSurcharge, bool isWeekend);

        decimal FinalPrice(int priceLowPeak, int pricePeak, bool isPeak,
            decimal weekendSurcharge, bool isWeekend, int voucherDiscount);
    }
}
