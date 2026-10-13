using BCrypt.Net;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Auth;
using M.Core.Base;
using ModelViews.Auth;

namespace Services
{
    /// <summary>
    /// AUTH service. INHERITANCE: extends <see cref="BaseService"/> so it shares
    /// the UoW + repository helper with every other service. Passwords are
    /// hashed with BCrypt; tokens come from the encapsulated <c>JwtGenerator</c>.
    /// </summary>
    public sealed class AuthService : BaseService, IAuthService
    {
        private readonly JwtGenerator _jwt;
        private readonly IGenericRepository<Role> _roles;

        public AuthService(IUnitOfWork uow,
            Microsoft.AspNetCore.Http.IHttpContextAccessor http,
            JwtGenerator jwt) : base(uow, http)
        {
            _jwt = jwt;
            _roles = Repository<Role>();
        }

        public async Task<LoginResponse> RegisterAsync(RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
                throw new BadRequestException("Email and password are required.");

            var userRepo = Repository<User>();
            var exists = await userRepo.GetByConditionAsync(u =>
                u.Email == request.Email);
            if (exists.Count > 0)
                throw new ConflictException("Email is already registered.");

            // Default to the Customer role (id 3 in the seeded Roles table).
            var customerRole = (await _roles.GetByConditionAsync(
                r => r.Name == "Customer" && r.IsActive)).FirstOrDefault();
            if (customerRole is null)
                throw new ConflictException("Default role not found; seed Roles first.");

            var user = new User
            {
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                RoleId = customerRole.RoleId,
                IsActive = true
            };
            _ = userRepo.AddAsync(user);
            await SaveAsync();

            return new LoginResponse
            {
                AccessToken = "",
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = customerRole.Name
            };
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var userRepo = Repository<User>();
            var users = await userRepo.GetByConditionAsync(u => u.Email == request.Email);
            var user = users.FirstOrDefault();
            if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new BadRequestException("Invalid email or password.");
            if (!user.IsActive)
                throw new ConflictException("Account is deactivated.");

            var role = await _roles.GetByIdAsync(user.RoleId);
            var (access, _, _) = _jwt.Generate(
                user.UserId, user.Email, user.FullName, role?.Name ?? "Customer");

            user.UpdatedAt = DateTime.UtcNow;
            userRepo.Update(user);
            await SaveAsync();

            return new LoginResponse
            {
                AccessToken = access,
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                Role = role?.Name ?? "Customer"
            };
        }
    }
}
