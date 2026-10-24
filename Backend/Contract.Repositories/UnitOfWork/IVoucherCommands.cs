namespace Contract.Repositories.UnitOfWork;

public interface IVoucherCommands
{
    Task<int> ReserveAsync(string code);
    Task<int> ReleaseAsync(int id);
    Task<int> DeleteRedemptionAsync(int redemptionId);
}
