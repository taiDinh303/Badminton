//using Contract.Services.Interface;
//using Microsoft.Extensions.Caching.Memory;

//namespace Services.Infrastructure
//{
//    public class OtpService : IOtpService
//    {
//        private readonly IMemoryCache _cache;

//        public OtpService(IMemoryCache cache)
//        {
//            _cache = cache;
//        }

//        public string GenerateConfirmationCode()
//        {
//            var random = new Random();
//            return random.Next(100000, 999999).ToString();
//        }

//        public Task StoreAsync(string email, string code, DateTime expiration)
//        {
//            // Tính khoảng thời gian tồn tại của OTP
//            var ttl = expiration - DateTime.UtcNow;

//            if (ttl <= TimeSpan.Zero)
//                ttl = TimeSpan.FromMinutes(5); // default fallback

//            // Lưu OTP vào cache
//            _cache.Set(email, code, ttl);

//            return Task.CompletedTask;
//        }

//        public Task<bool> ValidateAsync(string email, string code)
//        {
//            if (!_cache.TryGetValue(email, out string storedCode))
//                return Task.FromResult(false);

//            // Nếu đúng OTP, xóa luôn để không dùng lại
//            if (storedCode == code)
//                _cache.Remove(email);

//            return Task.FromResult(storedCode == code);
//        }
//    }
//}
