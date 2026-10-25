using Badminton.Core.Utils;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Branch;
using Badminton.Core.Base;
using Badminton.Core.Security;
using ModelViews.Branch;

namespace Services
{
    public sealed class BranchService : BaseService, IBranchService
    {
        private readonly IAuditService _audit;
        public BranchService(IUnitOfWork uow, ICurrentUser currentUser, IAuditService audit)
            : base(uow, currentUser) { _audit = audit; }

        private IGenericRepository<Branch> Repo => Repository<Branch>();

        public async Task<IReadOnlyList<BranchView>> GetAllAsync()
        {
            var branches = await Repo.GetByConditionAsync(b => b.DeletedAt == null);

            var activeCourts = await Repository<Court>()
                .GetByConditionAsync(c => c.IsActive && c.DeletedAt == null);
            var counts = activeCourts
                .GroupBy(c => c.BranchId)
                .ToDictionary(g => g.Key, g => g.Count());

            return branches
                .Select(b => new BranchView(
                    b.Id, b.Name, b.Address, b.IsActive,
                    b.OpenTime.ToString("HH\\:mm"), b.CloseTime.ToString("HH\\:mm"),
                    counts.TryGetValue(b.Id, out var n) ? n : 0))
                .ToList();
        }

        public async Task<BranchView> GetByIdAsync(int id)
        {
            var b = await MustGetAsync(id);
            var courtCount = await Repository<Court>()
                .CountByConditionAsync(c => c.BranchId == id && c.IsActive && c.DeletedAt == null);
            return new BranchView(b.Id, b.Name, b.Address, b.IsActive,
                b.OpenTime.ToString("HH\\:mm"), b.CloseTime.ToString("HH\\:mm"), courtCount);
        }

        public async Task<int> CreateAsync(CreateBranchRequest request, int actorId)
        {
            int result = 0;
            await Uow.RunInTransactionAsync(async () => { result = await CreateCoreAsync(request, actorId); });
            return result;
        }

        private async Task<int> CreateCoreAsync(CreateBranchRequest request, int actorId)
        {
            Validate(request.Name, request.Address, ParseTime(request.OpenTime), ParseTime(request.CloseTime));
            var dupes = await Repo.GetByConditionAsync(b => b.Name == request.Name && b.DeletedAt == null);
            if (dupes.Count > 0)
                throw new ConflictException($"Cơ sở '{request.Name}' đã tồn tại.");

            var b = new Branch
            {
                Name = request.Name,
                Address = request.Address,
                GeoLat = request.GeoLat,
                GeoLng = request.GeoLng,
                OpenTime = ParseTime(request.OpenTime),
                CloseTime = ParseTime(request.CloseTime)
            };
            await Repo.AddAsync(b);
            await SaveAsync();
            await _audit.LogAsync(actorId, "branch.create", "Branch", b.Id.ToString(), null, b);
            return b.Id;
        }

        public Task UpdateAsync(int id, UpdateBranchRequest request, int actorId) => Uow.RunInTransactionAsync(async () => {
            var branchId = id;
            await Uow.LockBookingRequestAsync($"branch-config:{branchId}");
            await UpdateCoreAsync(id, request, actorId);
            await new InventoryHistoryService(Uow).CaptureTodayAsync(branchId);
        });

        private async Task UpdateCoreAsync(int id, UpdateBranchRequest request, int actorId)
        {
            var b = await MustGetAsync(id);
            var open = request.OpenTime is null ? b.OpenTime : ParseTime(request.OpenTime);
            var close = request.CloseTime is null ? b.CloseTime : ParseTime(request.CloseTime);
            Validate(request.Name ?? b.Name, request.Address ?? b.Address, open, close);
            var today = HanoiTime.HanoiToday();
            var affected = await Repository<Booking>().CountByConditionAsync(x => x.BranchId == id && x.Date >= today && x.DeletedAt == null
                && (x.Status == BookingStatus.Pending || x.Status == BookingStatus.Confirmed)
                && (request.IsActive == false || (x.Slot != null && (x.Slot.StartTime < open || x.Slot.EndTime > close))));
            if (affected > 0) throw new ConflictException($"Có {affected} đơn bị ảnh hưởng. Xử lý đơn trước khi đóng hoặc thu hẹp giờ mở cửa.");
            var before = AuditCopy(b);
            if (request.Name is not null) b.Name = request.Name;
            if (request.Address is not null) b.Address = request.Address;
            if (request.IsActive.HasValue) b.IsActive = request.IsActive.Value;
            if (request.OpenTime is not null) b.OpenTime = ParseTime(request.OpenTime);
            if (request.CloseTime is not null) b.CloseTime = ParseTime(request.CloseTime);
            Validate(b.Name, b.Address, b.OpenTime, b.CloseTime);
            b.UpdatedAt = DateTime.UtcNow;
            Repo.Update(b);
            await SaveAsync();
            await _audit.LogAsync(actorId, "branch.update", "Branch", id.ToString(), before, b);
        }

        public Task DeleteAsync(int id, int actorId) => Uow.RunInTransactionAsync(async () => {
            var branchId = id;
            await Uow.LockBookingRequestAsync($"branch-config:{branchId}");
            await DeleteCoreAsync(id, actorId);
            await new InventoryHistoryService(Uow).CaptureTodayAsync(branchId);
        });

        private async Task DeleteCoreAsync(int id, int actorId)
        {
            var b = await MustGetAsync(id);
            if ((await GetImpactAsync(id)).Count > 0)
                throw new ConflictException("Cơ sở còn đơn đang hoạt động. Xử lý đơn trước khi xóa.");
            var before = AuditCopy(b);
            b.IsActive = false;
            b.DeletedAt = DateTime.UtcNow;
            b.UpdatedAt = DateTime.UtcNow;
            Repo.Update(b);
            await SaveAsync();
            await _audit.LogAsync(actorId, "branch.delete", "Branch", id.ToString(), before, b);
        }

        public async Task<ModelViews.Booking.ConfigurationImpact> GetImpactAsync(int id)
        {
            await MustGetAsync(id);
            await AuthorizeStaffBranchAsync(id, _currentUser.Id);
            var today = HanoiTime.HanoiToday();
            var rows = await Repository<Booking>().GetByConditionAsync(b => b.BranchId == id && b.Date >= today && b.DeletedAt == null
                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            return new(rows.Count, rows.OrderBy(b => b.Date).Take(10).Select(b => b.BookingCode).ToList());
        }

        private async Task<Branch> MustGetAsync(int id)
        {
            var b = await Repo.GetByIdAsync(id);
            if (b is null || b.DeletedAt.HasValue)
                throw new NotFoundException("Branch", id);
            return b;
        }

        private static TimeOnly ParseTime(string s) =>
            TimeOnly.TryParse(s, out var t) ? t : throw new BadRequestException("Giờ không hợp lệ.");

        private static void Validate(string name, string address, TimeOnly open, TimeOnly close)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100 || string.IsNullOrWhiteSpace(address) || address.Length > 255)
                throw new BadRequestException("Tên và địa chỉ cơ sở không hợp lệ.");
            if (close <= open) throw new BadRequestException("Giờ đóng cửa phải sau giờ mở cửa trong cùng ngày.");
        }

        private static object? AuditCopy(Branch b) => new
        {
            b.Name, b.Address, b.IsActive, b.OpenTime, b.CloseTime
        };
    }
}
