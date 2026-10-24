using Contract.Repositories.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Repositories.Context;

namespace Repositories.UnitOfWork;

internal sealed class VoucherCommands(BadmintonBookingDbContext db) : IVoucherCommands
{
    public Task<int> ReserveAsync(string code) => db.Vouchers
        .Where(v => v.Code == code && v.IsActive && v.UsedQty < v.TotalQty)
        .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedQty, v => v.UsedQty + 1));

    public Task<int> ReleaseAsync(int id) => db.Vouchers.Where(v => v.Id == id && v.UsedQty > 0)
        .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedQty, v => v.UsedQty - 1));

    public Task<int> DeleteRedemptionAsync(int redemptionId) => db.VoucherRedemptions
        .Where(r => r.Id == redemptionId).ExecuteDeleteAsync();
}
