using Badminton.Core.Base;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class CourtFavoriteService(IUnitOfWork uow, IOperationsAccess access) : ICourtFavoriteService
{
    public async Task<IReadOnlyList<FavoriteView>> FavoritesAsync()
    {
        await access.ActorAsync();
        var ids = (await uow.GetRepository<CourtFavorite>().GetByConditionAsync(f => f.UserId == access.ActorId)).Select(f => f.CourtId).ToArray();
        return (await uow.GetRepository<Court>().GetByConditionAsync(c => ids.Contains(c.Id) && c.DeletedAt == null && c.IsActive)).Select(c => new FavoriteView(c.Id, c.BranchId, c.Name)).ToArray();
    }
    public async Task FavoriteAsync(int courtId, bool enabled)
    {
        await access.ActorAsync();
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockBookingRequestAsync($"favorites:{access.ActorId}");
            var court = await uow.GetRepository<Court>().GetByIdAsync(courtId);
            if (court is null || court.DeletedAt.HasValue)
                throw new NotFoundException("Court", courtId);
            var repo = uow.GetRepository<CourtFavorite>();
            var row = (await repo.GetByConditionAsync(f => f.UserId == access.ActorId && f.CourtId == courtId)).SingleOrDefault();
            if (enabled && row is null)
            {
                if (await repo.CountByConditionAsync(f => f.UserId == access.ActorId) >= 100)
                    throw new ConflictException("Tối đa 100 sân yêu thích.");
                await repo.AddAsync(new()
                {
                    UserId = access.ActorId,
                    CourtId = courtId
                });
            }
            if (!enabled && row is not null)
                await repo.RemoveAsync(row);
            await uow.SaveChangesAsync();
        });
    }
}
