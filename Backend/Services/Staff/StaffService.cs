using BCrypt.Net;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Staff;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Security;

namespace Services
{
    public sealed class StaffService : BaseService, IStaffService
    {
        private readonly IAuditService _audit;
        public StaffService(IUnitOfWork uow, ICurrentUser currentUser, IAuditService audit)
            : base(uow, currentUser) { _audit = audit; }

        private IGenericRepository<User> Repo => Repository<User>();

        public async Task<int> CreateAsync(StaffCreateRequest request, int actorId)
        {
            int id = 0;
            await Uow.RunInTransactionAsync(async () => id = await CreateCoreAsync(request, actorId));
            return id;
        }

        private async Task<int> CreateCoreAsync(StaffCreateRequest request, int actorId)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber))
                throw new BadRequestException("Số điện thoại là bắt buộc.");
            AuthInput.Password(request.Password);
            request = request with { FullName = AuthInput.Name(request.FullName), PhoneNumber = AuthInput.Phone(request.PhoneNumber) };

            if (request.Role is not Role.Manager)
                throw new BadRequestException(
                    "Chỉ tạo được Manager. Hệ thống giữ đúng 1 nick Admin — " +
                    "không thể cấp role Admin cho nhân viên mới.");

            // "Exactly one active Admin" invariant.
            if (request.Role == Role.Admin)
            {
                var activeAdmins = await Repo.CountByConditionAsync(
                    u => u.Role == Role.Admin && u.Status == UserStatus.Active && u.DeletedAt == null);
                if (activeAdmins >= 1)
                    throw new ConflictException("Hệ thống chỉ cho phép DÙNG MỘT admin active. " +
                        "Hãy lock admin cũ trước khi tạo admin mới.");
            }

            var dupes = await Repo.GetByConditionAsync(u => u.PhoneNumber == request.PhoneNumber);
            if (dupes.Count > 0)
                throw new ConflictException("Số điện thoại này đã được đăng ký.");

            // A Manager MUST be assigned to a branch (1 manager = 1 cơ sở).
            if (request.Role == Role.Manager && request.BranchId is null)
                throw new BadRequestException("Manager phải được gán vào một cơ sở (BranchId).");

            var user = new User
            {
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role,
                Status = UserStatus.Active,
                IsVerified = true,
                BranchId = request.Role == Role.Manager ? request.BranchId : null
            };
            await Repo.AddAsync(user);
            try { await SaveAsync(); }
            catch (UniqueConstraintException)
            {
                throw new ConflictException("Vi phạm ràng buộc duy nhất (số điện thoại hoặc admin).");
            }
            await _audit.LogAsync(actorId, "staff.create", "User", user.Id.ToString(),
                null, new { user.FullName, user.PhoneNumber, user.Role, user.BranchId },
                $"Tạo {user.Role} {user.FullName}");
            return user.Id;
        }

        public Task SetStatusAsync(int userId, UserStatus status, int actorId) => Uow.RunInTransactionAsync(async () =>
        {
            await Uow.LockPointsUserAsync(userId);
            await SetStatusCoreAsync(userId, status, actorId);
        });

        private async Task SetStatusCoreAsync(int userId, UserStatus status, int actorId)
        {
            var u = await MustGetStaff(userId);
            var before = new { u.Status, u.Role };
            u.Status = status;
            u.SecurityStamp = Guid.NewGuid().ToString("N");
            u.PasswordRecoveryHash = null; u.PasswordRecoveryExpiresAt = null;
            u.UpdatedAt = DateTime.UtcNow;
            Repo.UpdateProperties(u, nameof(User.Status), nameof(User.SecurityStamp), nameof(User.PasswordRecoveryHash), nameof(User.PasswordRecoveryExpiresAt), nameof(User.UpdatedAt));
            await SaveAsync();
            await _audit.LogAsync(actorId, status == UserStatus.Locked ? "staff.lock" : "staff.unlock",
                "User", userId.ToString(), before, new { u.Status, u.Role },
                status == UserStatus.Locked ? "Khóa tài khoản" : "Mở khóa tài khoản");
        }

        public Task SetRoleAsync(int userId, Role role, int actorId) => Uow.RunInTransactionAsync(async () =>
        {
            await Uow.LockPointsUserAsync(userId);
            await SetRoleCoreAsync(userId, role, actorId);
        });

        private async Task SetRoleCoreAsync(int userId, Role role, int actorId)
        {
            var u = await MustGetStaff(userId);

            if (role == Role.Admin)
                throw new BadRequestException(
                    "Không thể cấp role Admin — hệ thống giữ đúng 1 nick Admin.");
            if (role is not Role.Manager)
                throw new BadRequestException("Chỉ đổi được role Manager (Admin không cấp, Customer không phải nhân viên).");
            // An active Admin cannot be demoted while the system needs exactly one admin.
            if (u.Role == Role.Admin && u.Status == UserStatus.Active)
            {
                var activeAdmins = await Repo.CountByConditionAsync(
                    x => x.Role == Role.Admin && x.Status == UserStatus.Active && x.DeletedAt == null);
                if (activeAdmins <= 1)
                    throw new ConflictException("Không thể hạ role admin duy nhất đang active.");
            }
            var before = new { u.Role, u.BranchId };
            u.Role = role;
            u.SecurityStamp = Guid.NewGuid().ToString("N");
            u.PasswordRecoveryHash = null; u.PasswordRecoveryExpiresAt = null;
            u.UpdatedAt = DateTime.UtcNow;
            Repo.UpdateProperties(u, nameof(User.Role), nameof(User.SecurityStamp), nameof(User.PasswordRecoveryHash), nameof(User.PasswordRecoveryExpiresAt), nameof(User.UpdatedAt));
            await SaveAsync();
            await _audit.LogAsync(actorId, "staff.role", "User", userId.ToString(),
                before, new { u.Role, u.BranchId }, $"Đổi role thành {role}");
        }

        public Task AssignBranchAsync(int userId, int branchId, int actorId) => Uow.RunInTransactionAsync(async () =>
        {
            await Uow.LockPointsUserAsync(userId);
            await AssignBranchCoreAsync(userId, branchId, actorId);
        });

        private async Task AssignBranchCoreAsync(int userId, int branchId, int actorId)
        {
            var u = await MustGetStaff(userId);
            if (u.Role != Role.Manager)
                throw new BadRequestException("Chỉ Manager được gán cơ sở.");

            // Validate the branch exists.
            var branch = await Repository<Branch>().GetByIdAsync(branchId);
            if (branch is null || !branch.IsActive)
                throw new NotFoundException("Branch", branchId);

            var branchSnapshot = u.BranchId;
            u.BranchId = branchId;
            u.SecurityStamp = Guid.NewGuid().ToString("N");
            u.PasswordRecoveryHash = null; u.PasswordRecoveryExpiresAt = null;
            u.UpdatedAt = DateTime.UtcNow;
            Repo.UpdateProperties(u, nameof(User.BranchId), nameof(User.SecurityStamp), nameof(User.PasswordRecoveryHash), nameof(User.PasswordRecoveryExpiresAt), nameof(User.UpdatedAt));
            await SaveAsync();
            await _audit.LogAsync(actorId, "staff.assign-branch", "User", userId.ToString(),
                new { BranchId = branchSnapshot }, new { NewBranchId = branchId },
                $"Gán cơ sở {branch.Name}");
        }

        public async Task<IReadOnlyList<StaffView>> ListAsync()
        {
            var staff = await Repo.GetByConditionAsync(
                u => u.Role != Role.Customer && u.DeletedAt == null);
            return staff.Select(u => new StaffView(
                u.Id, u.FullName, u.PhoneNumber, u.Role.ToString(),
                u.Status.ToString(), u.BranchId, u.IsVerified, u.CreatedAt)).ToList();
        }

        private async Task<User> MustGetStaff(int userId)
        {
            var u = await Repo.GetByIdAsync(userId);
            if (u is null || u.DeletedAt.HasValue)
                throw new NotFoundException("User", userId);
            if (u.Role == Role.Customer)
                throw new BadRequestException("Đây là tài khoản khách, không phải nhân viên.");
            return u;
        }
    }
}
