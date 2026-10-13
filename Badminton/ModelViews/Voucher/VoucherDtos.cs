namespace ModelViews.Voucher
{
    public record CreateVoucherRequest(string Code, string Type, int Value,
        int? MaxValue = null, int MinSpend = 0, int MaxPerUser = 1,
        int TotalQty = 1000, string? ValidFrom = null, string? ValidUntil = null);

    public record VoucherView(int Id, string Code, string Type, int Value, int? MaxValue,
        int MinSpend, int MaxPerUser, int TotalQty, int UsedQty, string? ValidFrom,
        string? ValidUntil, bool IsActive);
}
