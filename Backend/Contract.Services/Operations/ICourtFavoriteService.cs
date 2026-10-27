using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface ICourtFavoriteService
{
    Task<IReadOnlyList<FavoriteView>> FavoritesAsync();
    Task FavoriteAsync(int courtId, bool enabled);
}
