using BCrypt.Net;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Auth;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Security;
using ModelViews.Auth;

namespace Services
{
    public sealed class AuthService : BaseService, IAuthService
    {
        private readonly JwtGenerator _jwt;
        private readonly ILoginAttemptTracker _attempts;

        public AuthService(IUnitOfWork uow, ICurrentUser currentUser, JwtGenerator jwt, ILoginAttemptTracker attempts)
            : base(uow, currentUser)
        {
            _jwt = jwt;
            _attempts = attempts;
        }

        // ── Register ─────────────────────────────────────────────────────

        public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
        {
            request = request with { PhoneNumber = AuthInput.Phone(request.PhoneNumber), FullName = AuthInput.Name(request.FullName) };
            AuthInput.Password(request.Password);
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                throw new BadRequestException("Số điện thoại là bắt buộc.");

            if (request.Role != RoleDto.Customer)
                throw new ConflictException(
                    "Manager/Admin được tạo bởi admin, không thể tự đăng ký.");

            var users = Repository<User>();
            var dupes = await users.GetByConditionAsync(u => u.PhoneNumber == request.PhoneNumber);
            if (dupes.Count > 0)
                throw new ConflictException("Số điện thoại này đã được đăng ký.");

            var user = new User
            {
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = Role.Customer,
                Status = UserStatus.Active,
                IsVerified = true
            };

            await users.AddAsync(user);
            try { await SaveAsync(); }
            catch (UniqueConstraintException)
            {
                throw new ConflictException("Số điện thoại đã được đăng ký.");
            }

            var (access, _, _) = _jwt.Generate(user.Id, user.FullName, user.PhoneNumber,
                user.Role.ToString(), user.SecurityStamp);
            return new LoginResponse(user.Id, user.FullName, user.PhoneNumber,
                user.Role.ToString(), access, _jwt.ExpireMinutes());
        }

        // ── Login ────────────────────────────────────────────────────────

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var phone = AuthInput.Phone(request.PhoneNumber);

            // Rate-limit check (in-memory; 5 attempts/10min → lock 15min).
            if (_attempts.IsLocked(phone))
                throw new ConflictException(
                    "Quá nhiều lần thử sai. Vui lòng thử lại sau 15 phút.");

            var users = Repository<User>();
            var found = (await users.GetByConditionAsync(
                u => u.PhoneNumber == phone)).FirstOrDefault();

            if (found is null || !BCrypt.Net.BCrypt.Verify(request.Password, found.PasswordHash))
            {
                _attempts.RecordFailure(phone);
                throw new BadRequestException("Số điện thoại hoặc mật khẩu không đúng.");
            }

            // Successful auth → clear any accumulated failures for this phone.
            _attempts.Clear(phone);

            if (found.Status != UserStatus.Active)
                throw new ConflictException("Tài khoản đã bị khóa.");
            if (!found.IsVerified)
                throw new ConflictException("Vui lòng xác minh số điện thoại.");

            var (access, _, _) = _jwt.Generate(found.Id, found.FullName, found.PhoneNumber,
                found.Role.ToString(), found.SecurityStamp);
            return new LoginResponse(found.Id, found.FullName, found.PhoneNumber,
                found.Role.ToString(), access, _jwt.ExpireMinutes());
        }

        public Task ChangePasswordAsync(ChangePasswordRequest request) => Uow.RunInTransactionAsync(async () =>
        {
            AuthInput.Password(request.CurrentPassword);
            AuthInput.Password(request.NewPassword);
            if (request.CurrentPassword == request.NewPassword) throw new BadRequestException("Mật khẩu mới phải khác mật khẩu hiện tại.");
            await Uow.LockPointsUserAsync(_currentUser.Id);
            var user = await RequireUserAsync();
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                throw new BadRequestException("Mật khẩu hiện tại không đúng.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.UpdatedAt = DateTime.UtcNow;
            Repository<User>().Update(user);
            await SaveAsync();
        });

        public Task RevokeSessionsAsync() => Uow.RunInTransactionAsync(async () =>
        {
            await Uow.LockPointsUserAsync(_currentUser.Id);
            var user = await RequireUserAsync();
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.UpdatedAt = DateTime.UtcNow;
            Repository<User>().Update(user);
            await SaveAsync();
        });

        private async Task<User> RequireUserAsync()
        {
            var user = await Repository<User>().GetByIdAsync(_currentUser.Id);
            if (user is null || user.DeletedAt.HasValue || user.Status != UserStatus.Active || !user.IsVerified)
                throw new ConflictException("Tài khoản không còn hoạt động.");
            // Recheck after the account lock as a second concurrent password change/logout may have revoked this request.
            if ((_currentUser.Principal.FindFirst("sv")?.Value ?? "") != (user.SecurityStamp ?? ""))
                throw new ConflictException("Phiên đăng nhập đã bị thu hồi.");
            return user;
        }


    }
}
