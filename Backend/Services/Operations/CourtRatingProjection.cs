using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
namespace Services.Operations;

// Caller owns court-reviews lock and transaction; source rows remain authoritative.
public sealed class CourtRatingProjection(IUnitOfWork uow) : ICourtRatingProjection
{
    public async Task RebuildAsync(int courtId)
    {
        var reviews = uow.GetRepository<Review>();
        var count = await reviews.CountByConditionAsync(r => r.CourtId == courtId && !r.IsHidden);
        var stars = await reviews.SumIntByConditionAsync(r => r.CourtId == courtId && !r.IsHidden, r => r.Rating);
        var court = await uow.GetRepository<Court>().GetByIdAsync(courtId);
        if (court is null)
            return;
        court.RatingCount = count;
        court.RatingAvg = count == 0 ? 0 : Math.Round((double)stars / count, 2);
        court.UpdatedAt = DateTime.UtcNow;
        uow.GetRepository<Court>().UpdateProperties(court, nameof(Court.RatingAvg), nameof(Court.RatingCount), nameof(Court.UpdatedAt));
        await uow.SaveChangesAsync();
    }
}
