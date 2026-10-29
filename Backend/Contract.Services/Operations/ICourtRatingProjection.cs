namespace Contract.Services.Operations;

public interface ICourtRatingProjection
{
    Task RebuildAsync(int courtId);
}
