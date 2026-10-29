using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IMediaService
{
    Task<IReadOnlyList<MediaView>> ListAsync(int? courtId, int? reviewId);
    Task<MediaView> UploadAsync(byte[] bytes, int? courtId, int? reviewId, CancellationToken ct);
    Task<(byte[] Bytes, string ContentType)> ReadAsync(int id, CancellationToken ct);
    Task DeleteAsync(int id, CancellationToken ct);
    Task SetAvatarAsync(int id);
}
