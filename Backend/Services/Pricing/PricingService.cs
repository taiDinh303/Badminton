using Contract.Services.Pricing;

namespace Services
{
    public sealed class PricingService : IPricingService
    {
        public decimal BasePrice(int priceLowPeak, int pricePeak, bool isPeak) =>
            isPeak ? pricePeak : priceLowPeak;

        public decimal WithWeekend(int priceLowPeak, int pricePeak, bool isPeak,
            decimal weekendSurcharge, bool isWeekend)
        {
            var basePrice = BasePrice(priceLowPeak, pricePeak, isPeak);
            return isWeekend ? basePrice * (1m + weekendSurcharge) : basePrice;
        }

        public decimal FinalPrice(int priceLowPeak, int pricePeak, bool isPeak,
            decimal weekendSurcharge, bool isWeekend, int voucherDiscount)
        {
            var total = WithWeekend(priceLowPeak, pricePeak, isPeak, weekendSurcharge, isWeekend);
            return Math.Max(0, total - voucherDiscount);
        }
    }
}
