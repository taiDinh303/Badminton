using BCrypt.Net;
using Contract.Repositories.Entity;
using Contract.Repositories.IOUW;
using Contract.Services.Interface;
using ModelViews.Auth;

namespace Services.Service
{
    public class AuthService : IAuthService
    {
        private readonly IUOW _uow;
        private readonly ITokenService _tokenService;

        public AuthService(IUOW uow, ITokenService tokenService)
        {
            _uow = uow;
            _tokenService = tokenService;
        }

        public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
        {
            var exists = await _uow.Users.ExistsByEmailAsync(request.Email);

            if (exists)
                throw new Exception("Email đã tồn tại.");

            var user = new User
            {
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Users.CreateAsync(user);

            var createdUser = await _uow.Users.GetByEmailAsync(user.Email);

            return new LoginResponse
            {
                UserId = createdUser!.UserId,
                FullName = createdUser.FullName,
                Email = createdUser.Email,
                Role = createdUser.Role.Name
            };
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await _uow.Users.GetByEmailAsync(request.Email);

            if (user == null)
                throw new Exception("Email hoặc mật khẩu không đúng.");

            var validPassword = BCrypt.Net.BCrypt.Verify(
                request.Password,
                user.PasswordHash);

            if (!validPassword)
                throw new Exception("Email hoặc mật khẩu không đúng.");

            var accessToken = _tokenService.GenerateToken(user);

            return new LoginResponse
            {
                AccessToken = accessToken,
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = user.Role.Name
            };
        }
    }
}
