using Contract.Services.Operations;
namespace Badminton.API.Adapters;

public sealed class FileMediaStore : IMediaStore
{
    private readonly string _root;
    public FileMediaStore(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Media:Directory"];
        if (environment.IsProduction() && string.IsNullOrWhiteSpace(configured))
            _root = "";
        else
            _root = Path.GetFullPath(configured ?? Path.Combine(environment.ContentRootPath, "private-media"));
    }
    private string Resolve(string key)
    {
        if (_root.Length == 0)
            throw new Badminton.Core.Base.ConflictException("Upload chưa được cấu hình thư mục lưu bền vững.");
        if (!Guid.TryParseExact(key, "N", out _))
            throw new InvalidOperationException("Invalid media key.");
        return Path.Combine(_root, key);
    }
    public async Task WriteAsync(string key, byte[] bytes, CancellationToken ct)
    {
        var path = Resolve(key);
        Directory.CreateDirectory(_root);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
        try
        {
            await stream.WriteAsync(bytes, ct);
        }
        catch { stream.Close(); File.Delete(path); throw; }
    }
    public Task<byte[]> ReadAsync(string key, CancellationToken ct) => File.ReadAllBytesAsync(Resolve(key), ct);
    public Task DeleteAsync(string key, CancellationToken ct)
    {
        File.Delete(Resolve(key));
        return Task.CompletedTask;
    }
}
