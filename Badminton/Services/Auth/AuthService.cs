using BCrypt.Net;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Auth;
using M.Core.Base;
using M.Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelViews.Auth;

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

        public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                throw new BadRequestException("So dien thoai la bat buoc.");

            if (request.Role != RoleDto.Customer)
                throw new ConflictException(
                    "Manager/Admin duoc tao boi admin, khong the tu dang ky.");

            var users = Repository<User>();
            var dupes = await users.GetByConditionAsync(u => u.PhoneNumber == request.PhoneNumber);
            if (dupes.Count > 0)
                throw new ConflictException("So dien thoai nay da duoc dang ky.");

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
                    throw new ConflictException("Dung luong admin/so dien thoai bi loi rang buoc.");
                throw;
            }

            var (access, _, _) = _jwt.Generate(user.Id, user.FullName, user.PhoneNumber,
                user.Role.ToString());
            return new LoginResponse(user.Id, user.FullName, user.PhoneNumber,
                user.Role.ToString(), access, _jwt.ExpireMinutes());
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var users = Repository<User>();
            var found = (await users.GetByConditionAsync(
                u => u.PhoneNumber == request.PhoneNumber)).FirstOrDefault();
            if (found is null || !BCrypt.Net.BCrypt.Verify(request.Password, found.PasswordHash))
                throw new BadRequestException("So dien thoai hoac mat khau khong dung.");
            if (found.Status != UserStatus.Active)
                throw new ConflictException("Tai khoan da bi hoa.");
            if (!found.IsVerified)
                throw new ConflictException("Vui long xac minh so dien thoai.");

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
