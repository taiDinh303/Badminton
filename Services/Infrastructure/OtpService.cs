using Contract.Services.Interface;
using Microsoft.Extensions.Caching.Memory;

namespace Services.Infrastructure
{
    public class OtpService : IOtpService
    {
        private readonly IMemoryCache _cache;

        public OtpService(IMemoryCache cache)
        {
            _cache = cache;
        }

        public string GenerateConfirmationCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        public Task StoreAsync(string key, string code, DateTime expiration)
        {
            var ttl = expiration - DateTime.UtcNow;

            if (ttl <= TimeSpan.Zero)
                ttl = TimeSpan.FromMinutes(5);

            _cache.Set(key, code, ttl);

            return Task.CompletedTask;
        }

        public Task<bool> ValidateAsync(string key, string code)
        {
            if (!_cache.TryGetValue(key, out string? storedCode))
                return Task.FromResult(false);

            if (storedCode == code)
                _cache.Remove(key);

            return Task.FromResult(storedCode == code);
        }
    }
}