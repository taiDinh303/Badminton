using Contract.Services.Report;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Maintenance;
using Contract.Services.Booking;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Security;

namespace Services
{
    public sealed class MaintenanceService : BaseService, IMaintenanceService
    {
        private readonly IAuditService _audit;
        private readonly IInventoryHistoryService _inventory;
        private readonly IBookingService _bookings;
        public MaintenanceService(IUnitOfWork uow, ICurrentUser currentUser, IAuditService audit,
            IBookingService bookings, IInventoryHistoryService inventory)
            : base(uow, currentUser) { _audit = audit; _bookings = bookings; _inventory = inventory; }

        public async Task SetAsync(int courtId, DateOnly date, CourtDateStatus status,
            int actorId, string? note = null, TimeSpan? startTime = null, TimeSpan? endTime = null)
        {
            var court = await Repository<Court>().GetByIdAsync(courtId);
            if (court is null || !court.IsActive) throw new NotFoundException("Court", courtId);
            await AuthorizeStaffBranchAsync(court.BranchId, actorId);
            if (!Enum.IsDefined(status)) throw new BadRequestException("Trạng thái sân không hợp lệ.");
            if (startTime.HasValue != endTime.HasValue || (startTime.HasValue &&
                (startTime < TimeSpan.Zero || endTime >= TimeSpan.FromDays(1) || startTime >= endTime)))
                throw new BadRequestException("Chọn đủ giờ bắt đầu/kết thúc, giờ kết thúc phải sau giờ bắt đầu.");
            if (status == CourtDateStatus.Open) { startTime = null; endTime = null; }

            CourtDateStatusEntry row = null!;
            object? before = null;
            await Uow.RunInTransactionAsync(async () =>
            {
                await LockCourtDateAsync(courtId, date);
                var repo = Repository<CourtDateStatusEntry>();
                var existing = (await repo.GetByConditionAsync(r => r.CourtId == courtId && r.Date == date)).FirstOrDefault();
                row = existing ?? new CourtDateStatusEntry { CourtId = courtId, Date = date };
                before = new { row.Status, row.Note, row.StartTime, row.EndTime };
                row.Status = status; row.Note = note; row.StartTime = startTime; row.EndTime = endTime;
                row.CreatedById = actorId; row.UpdatedAt = DateTime.UtcNow;
                if (existing is null) await repo.AddAsync(row); else repo.Update(row);
                await SaveAsync();
            await _audit.LogAsync(actorId, status == CourtDateStatus.Open ? "maintenance.open" : "maintenance.lock",
                "CourtDateStatus", $"{courtId}@{date:yyyy-MM-dd}", before,
                new { row.Status, row.Note, row.StartTime, row.EndTime }, note);
            });
            await _inventory.CaptureTodayAsync(court.BranchId);


            if (status != CourtDateStatus.Open)
            {
                var slots = await Repository<TimeSlot>().GetAllAsync();
                var slotIds = slots.Where(s => row.Blocks(s.StartTime, s.EndTime)).Select(s => s.Id).ToList();
                var affected = await Repository<Booking>().GetByConditionAsync(b => b.CourtId == courtId
                    && b.Date == date && slotIds.Contains(b.SlotId)
                    && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
                foreach (var b in affected)
                {
                    try
                    {
                        await _bookings.CancelForMaintenanceAsync(b.Id, actorId, new(b.CourtId, b.Date, b.SlotId));
                    }
                    catch (Exception)
                    {
                        throw new ConflictException($"Đã khóa sân nhưng chưa xử lý xong đơn {b.BookingCode}. Lưu lại cùng lịch để thử lại hoặc kiểm tra giao dịch hoàn tiền.");
                    }
                }
            }
        }

        public async Task<CourtDateStatus> GetStatusAsync(int courtId, DateOnly date)
            => (await GetScheduleAsync(courtId, date)).Status;

        public async Task<MaintenanceView> GetScheduleAsync(int courtId, DateOnly date)
        {
            var court = await Repository<Court>().GetByIdAsync(courtId) ?? throw new NotFoundException("Court", courtId);
            await AuthorizeStaffBranchAsync(court.BranchId, _currentUser.Id);
            var row = (await Repository<CourtDateStatusEntry>().GetByConditionAsync(r => r.CourtId == courtId && r.Date == date)).FirstOrDefault();
            return new(row?.Status ?? CourtDateStatus.Open, row?.StartTime?.ToString(@"hh\:mm"), row?.EndTime?.ToString(@"hh\:mm"), row?.Note);
        }
    }
}
