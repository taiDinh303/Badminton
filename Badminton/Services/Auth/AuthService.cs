using BCrypt.Net;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Auth;
using M.Core.Base;
using M.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelViews.Auth;
using System.Collections.Concurrent;

namespace Services
{
    public sealed class AuthService : BaseService, IAuthService
    {
        private readonly JwtGenerator _jwt;

        public AuthService(IUnitOfWork uow, IHttpContextAccessor http, JwtGenerator jwt)
            : base(uow, http)
        {
            _jwt = jwt;
        }

        private static readonly ConcurrentDictionary<string, List<DateTime>> _failures
            = new();
        private const int MaxAttempts = 5;
        private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

        private static bool IsLocked(string phone)
        {
            if (!_failures.TryGetValue(phone, out var times)) return false;
            var cutoff = DateTime.UtcNow.AddSeconds(-FailureWindow.TotalSeconds);
            var recent = times.Count(t => t >= cutoff);
            return recent >= MaxAttempts;
        }

        private static void RecordFailure(string phone)
        {
            _failures.AddOrUpdate(phone,
                _ => new List<DateTime> { DateTime.UtcNow },
                (_, list) => { list.Add(DateTime.UtcNow); return list; });
        }

        private static void ClearFailures(string phone) =>
            _failures.TryRemove(phone, out _);

        // ── Register ─────────────────────────────────────────────────────

        public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
        {
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

            _ = users.AddAsync(user);
            try { await SaveAsync(); }
            catch (DbUpdateException ex)
            {
                if (IsUniqueViolation(ex))
                    throw new ConflictException("Dùng số điện thoại đã tồn tại (vi phạm unique).");
                throw;
            }

            var (access, _, _) = _jwt.Generate(user.Id, user.FullName, user.PhoneNumber,
                user.Role.ToString());
            return new LoginResponse(user.Id, user.FullName, user.PhoneNumber,
                user.Role.ToString(), access, _jwt.ExpireMinutes());
        }

        // ── Login ────────────────────────────────────────────────────────

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var phone = request.PhoneNumber;

            // Rate-limit check (in-memory; 5 attempts/10min → lock 15min).
            if (IsLocked(phone))
                throw new ConflictException(
                    "Quá nhiều lần thử sai. Vui lòng thử lại sau 15 phút.");

            var users = Repository<User>();
            var found = (await users.GetByConditionAsync(
                u => u.PhoneNumber == phone)).FirstOrDefault();

            if (found is null || !BCrypt.Net.BCrypt.Verify(request.Password, found.PasswordHash))
            {
                RecordFailure(phone);
                throw new BadRequestException("Số điện thoại hoặc mật khẩu không đúng.");
            }

            // Successful auth → clear any accumulated failures for this phone.
            ClearFailures(phone);

            if (found.Status != UserStatus.Active)
                throw new ConflictException("Tài khoản đã bị khóa.");
            if (!found.IsVerified)
                throw new ConflictException("Vui lòng xác minh số điện thoại.");

            found.UpdatedAt = DateTime.UtcNow;
            users.Update(found);
            await SaveAsync();

            var (access, _, _) = _jwt.Generate(found.Id, found.FullName, found.PhoneNumber,
                found.Role.ToString());
            return new LoginResponse(found.Id, found.FullName, found.PhoneNumber,
                found.Role.ToString(), access, _jwt.ExpireMinutes());
        }

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException is Microsoft.Data.SqlClient.SqlException sql
            && sql.Number is 2601 or 2627;
    }
}
