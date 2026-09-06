using BCrypt.Net;
using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Contract.Services.Interface;

namespace Services.Service
{
    public class VerificationService : IVerificationService
    {
        private readonly IUOW _uow;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;

        public VerificationService(
            IUOW uow,
            IEmailService emailService,
            ISmsService smsService)
        {
            _uow = uow;
            _emailService = emailService;
            _smsService = smsService;
        }

        public async Task SendEmailVerificationAsync(int userId)
        {
            var user = await _uow.Users.GetByIdAsync(userId);

            if (user == null)
                throw new Exception("Không tìm thấy người dùng.");

            var code = await CreateVerificationCodeAsync(
                userId,
                "Email");

            await _emailService.SendVerificationCodeAsync(
                user.Email,
                user.FullName,
                code);
        }

        public async Task SendPhoneVerificationAsync(int userId)
        {
            var user = await _uow.Users.GetByIdAsync(userId);

            if (user == null)
                throw new Exception("Không tìm thấy người dùng.");

            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
                throw new Exception("Người dùng chưa có số điện thoại.");

            var code = await CreateVerificationCodeAsync(
                userId,
                "Phone");

            await _smsService.SendVerificationCodeAsync(
                user.PhoneNumber,
                code);
        }

        public async Task VerifyEmailAsync(
            int userId,
            string code)
        {
            await VerifyAsync(
                userId,
                "Email",
                code);
        }

        public async Task VerifyPhoneAsync(
            int userId,
            string code)
        {
            await VerifyAsync(
                userId,
                "Phone",
                code);
        }

        private async Task<string> CreateVerificationCodeAsync(
            int userId,
            string type)
        {
            var code = Random.Shared
                .Next(100000, 1000000)
                .ToString();

            var verificationCode = new VerificationCode
            {
                UserId = userId,
                CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
                Type = type,
                ExpiredAt = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.VerificationCodes
                .CreateAsync(verificationCode);

            await _uow.SaveChangesAsync();

            return code;
        }

        private async Task VerifyAsync(
            int userId,
            string type,
            string code)
        {
            var verificationCode =
                await _uow.VerificationCodes.GetLatestAsync(
                    userId,
                    type);

            if (verificationCode == null)
                throw new Exception(
                    "Không tìm thấy mã xác thực.");

            if (verificationCode.IsUsed)
                throw new Exception(
                    "Mã xác thực đã được sử dụng.");

            if (verificationCode.ExpiredAt < DateTime.UtcNow)
                throw new Exception(
                    "Mã xác thực đã hết hạn.");

            var validCode = BCrypt.Net.BCrypt.Verify(
                code,
                verificationCode.CodeHash);

            if (!validCode)
                throw new Exception(
                    "Mã xác thực không đúng.");

            var user = await _uow.Users
                .GetByIdAsync(userId);

            if (user == null)
                throw new Exception(
                    "Không tìm thấy người dùng.");

            verificationCode.IsUsed = true;

            if (type == "Email")
                user.IsEmailVerified = true;

            if (type == "Phone")
                user.IsPhoneVerified = true;

            await _uow.VerificationCodes
                .UpdateAsync(verificationCode);

            await _uow.SaveChangesAsync();
        }
    }
}