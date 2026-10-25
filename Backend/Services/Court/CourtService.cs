using Badminton.Core.Utils;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Court;
using Badminton.Core.Base;
using Badminton.Core.Security;
using ModelViews.Court;

namespace Services
{
    public sealed class CourtService : BaseService, ICourtService
    {
        private readonly IAuditService _audit;
        public CourtService(IUnitOfWork uow, ICurrentUser currentUser, IAuditService audit)
            : base(uow, currentUser) { _audit = audit; }

        private IGenericRepository<Court> Repo => Repository<Court>();

        public async Task<IReadOnlyList<CourtView>> GetAllAsync(int? branchId = null)
        {
            var courts = branchId is { } bid
                ? await Repo.GetByConditionAsync(c => c.BranchId == bid && c.IsActive && c.DeletedAt == null)
                : await Repo.GetByConditionAsync(c => c.IsActive && c.DeletedAt == null);
            return courts.Select(ToView).ToList();
        }

        public async Task<CourtView> GetByIdAsync(int id)
        {
            var c = await MustGetAsync(id);
            return ToView(c);
        }

        public async Task<int> CreateAsync(CreateCourtRequest request, int actorId)
        {
            int result = 0;
            await Uow.RunInTransactionAsync(async () => {
                await Uow.LockBookingRequestAsync($"branch-config:{request.BranchId}");
                result = await CreateCoreAsync(request, actorId);
                await new InventoryHistoryService(Uow).CaptureTodayAsync(request.BranchId);
            });
            return result;
        }

        private async Task<int> CreateCoreAsync(CreateCourtRequest request, int actorId)
        {
            Validate(request.Name, request.PriceLowPeak, request.PricePeak, request.WeekendSurcharge, request.Capacity);
            var dupes = await Repo.GetByConditionAsync(
                c => c.BranchId == request.BranchId && c.Code == request.Code && c.DeletedAt == null);
            if (dupes.Count > 0)
                throw new ConflictException($"Sân '{request.Code}' đã tồn tại ở cơ sở {request.BranchId}.");

            var c = new Court
            {
                BranchId = request.BranchId,
                Code = request.Code,
                Name = request.Name,
                PricePeak = request.PricePeak,
                PriceLowPeak = request.PriceLowPeak,
                WeekendSurcharge = request.WeekendSurcharge,
                Capacity = request.Capacity,
                Surface = ParseSurface(request.Surface),
                Amenities = request.Amenities
            };
            await Repo.AddAsync(c);
            await SaveAsync();
            await _audit.LogAsync(actorId, "court.create", "Court", c.Id.ToString(), null, c);
            return c.Id;
        }

        public Task UpdateAsync(int id, UpdateCourtRequest request, int actorId) => Uow.RunInTransactionAsync(async () => {
            var branchId = (await MustGetAsync(id)).BranchId;
            await Uow.LockBookingRequestAsync($"branch-config:{branchId}");
            await UpdateCoreAsync(id, request, actorId);
            await new InventoryHistoryService(Uow).CaptureTodayAsync(branchId);
        });

        private async Task UpdateCoreAsync(int id, UpdateCourtRequest request, int actorId)
        {
            var c = await MustGetAsync(id);
            Validate(request.Name ?? c.Name, request.PriceLowPeak ?? c.PriceLowPeak,
                request.PricePeak ?? c.PricePeak, request.WeekendSurcharge ?? c.WeekendSurcharge,
                request.Capacity ?? c.Capacity);
            if (request.IsActive == false && (await GetImpactAsync(id)).Count > 0)
                throw new ConflictException("Sân còn đơn đang hoạt động. Xử lý đơn hoặc dùng lịch bảo trì trước khi vô hiệu.");
            var today = HanoiTime.HanoiToday();
            if (request.Capacity is { } capacity && await Repository<Booking>().CountByConditionAsync(b => b.CourtId == id
                && b.Date >= today && b.DeletedAt == null && b.PlayerCount > capacity
                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)) > 0)
                throw new ConflictException("Sức chứa mới thấp hơn số khách của đơn đang hoạt động. Xử lý đơn trước khi giảm sức chứa.");
            var before = AuditCopy(c);
            if (request.Name is not null) c.Name = request.Name;
            if (request.PriceLowPeak.HasValue) c.PriceLowPeak = request.PriceLowPeak.Value;
            if (request.PricePeak.HasValue) c.PricePeak = request.PricePeak.Value;
            if (request.WeekendSurcharge.HasValue) c.WeekendSurcharge = request.WeekendSurcharge.Value;
            if (request.Capacity.HasValue) c.Capacity = request.Capacity.Value;
            if (request.Surface is not null) c.Surface = ParseSurface(request.Surface);
            if (request.Amenities is not null) c.Amenities = request.Amenities;
            if (request.IsActive.HasValue) c.IsActive = request.IsActive.Value;
            c.UpdatedAt = DateTime.UtcNow;
            Repo.UpdateProperties(c, nameof(Court.Name), nameof(Court.PriceLowPeak), nameof(Court.PricePeak),
                nameof(Court.WeekendSurcharge), nameof(Court.Capacity), nameof(Court.Surface), nameof(Court.Amenities), nameof(Court.IsActive), nameof(Court.UpdatedAt));
            await SaveAsync();
            await _audit.LogAsync(actorId, "court.update", "Court", id.ToString(), before, c,
                "Chỉnh giá/cấu hình sân (phi-phiên bản, giữ snapshot booking đã xác nhận)");
        }

        public Task DeleteAsync(int id, int actorId) => Uow.RunInTransactionAsync(async () => {
            var branchId = (await MustGetAsync(id)).BranchId;
            await Uow.LockBookingRequestAsync($"branch-config:{branchId}");
            await DeleteCoreAsync(id, actorId);
            await new InventoryHistoryService(Uow).CaptureTodayAsync(branchId);
        });

        private async Task DeleteCoreAsync(int id, int actorId)
        {
            var c = await MustGetAsync(id);
            if ((await GetImpactAsync(id)).Count > 0)
                throw new ConflictException("Sân còn đơn đang hoạt động. Xử lý đơn hoặc dùng lịch bảo trì trước khi xóa.");
            var before = AuditCopy(c);
            c.IsActive = false;
            c.DeletedAt = DateTime.UtcNow;
            c.UpdatedAt = DateTime.UtcNow;
            Repo.UpdateProperties(c, nameof(Court.IsActive), nameof(Court.DeletedAt), nameof(Court.UpdatedAt));
            await SaveAsync();
            await _audit.LogAsync(actorId, "court.delete", "Court", id.ToString(), before, c);
        }

        public async Task<ModelViews.Booking.ConfigurationImpact> GetImpactAsync(int id)
        {
            var court = await MustGetAsync(id);
            await AuthorizeStaffBranchAsync(court.BranchId, _currentUser.Id);
            var today = HanoiTime.HanoiToday();
            var rows = await Repository<Booking>().GetByConditionAsync(b => b.CourtId == id && b.Date >= today && b.DeletedAt == null
                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
            return new(rows.Count, rows.OrderBy(b => b.Date).Take(10).Select(b => b.BookingCode).ToList());
        }

        private async Task<Court> MustGetAsync(int id)
        {
            var c = await Repo.GetByIdAsync(id);
            if (c is null || c.DeletedAt.HasValue) throw new NotFoundException("Court", id);
            return c;
        }

        private static void Validate(string name, int lowPeak, int peak, decimal surcharge, int capacity)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
                throw new BadRequestException("Tên sân phải có từ 1 đến 100 ký tự.");
            if (lowPeak < 0 || peak <= 0)
                throw new BadRequestException("Giá thường không được âm và giá cao điểm phải dương.");
            if (capacity < 1)
                throw new BadRequestException("Sức chứa sân phải lớn hơn 0.");
            if (surcharge < 0 || surcharge > 999999.9999m
                || Math.Max(lowPeak, peak) * (1m + surcharge) > int.MaxValue)
                throw new BadRequestException("Phụ thu không được âm và giá sau phụ thu phải nằm trong giới hạn số tiền hợp lệ.");
        }

        private static CourtSurface ParseSurface(string s) =>
            Enum.TryParse<CourtSurface>(s, ignoreCase: true, out var v) ? v : CourtSurface.Sindalo;

        private static CourtView ToView(Court c) => new(
            c.Id, c.BranchId, c.Code, c.Name, c.PriceLowPeak, c.PricePeak,
            c.WeekendSurcharge, c.Capacity, c.Surface.ToString(), c.IsActive,
            c.RatingAvg, c.RatingCount);

        private static object? AuditCopy(Court c) => new
        {
            c.Name, c.PriceLowPeak, c.PricePeak, c.WeekendSurcharge, c.Capacity,
            c.Surface, c.Amenities, c.IsActive
        };
    }
}
