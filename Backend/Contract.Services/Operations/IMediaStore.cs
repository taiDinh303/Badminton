using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IMediaStore
{
    Task WriteAsync(string key, byte[] bytes, CancellationToken ct);
    Task<byte[]> ReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
