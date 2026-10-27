using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class StaffWorkService(IUnitOfWork uow, IOperationsAccess access, IAuditService audit) : IStaffWorkService
{
    public async Task<IReadOnlyList<ShiftStaffView>> StaffAsync(int? branchId)
    {
        var branch = await access.StaffBranchAsync(branchId);
        var users = await uow.GetRepository<User>().GetByConditionAsync(u => u.DeletedAt == null && u.Status == UserStatus.Active && u.Role == Role.Manager && (branch == null || u.BranchId == branch));
        return users.Select(u => new ShiftStaffView(u.Id, u.FullName, u.BranchId)).ToArray();
    }
    public async Task<IReadOnlyList<ShiftView>> ShiftsAsync(DateOnly from, DateOnly to, int? branchId)
    {
        OperationInput.DateRange(from, to);
        var branch = await access.StaffBranchAsync(branchId);
        var start = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var rows = await uow.GetRepository<StaffShift>().GetByConditionAsync(s => s.DeletedAt == null && s.StartsAtUtc < end && s.EndsAtUtc > start && (branch == null || s.BranchId == branch));
        var ids = rows.Select(s => s.StaffId).Distinct().ToArray();
        var users = (await uow.GetRepository<User>().GetByConditionAsync(u => ids.Contains(u.Id))).ToDictionary(u => u.Id, u => u.FullName);
        return rows.OrderBy(s => s.StartsAtUtc).Select(s => new ShiftView(s.Id, s.BranchId, s.StaffId, users.GetValueOrDefault(s.StaffId) ?? "", s.StartsAtUtc, s.EndsAtUtc, s.Note)).ToArray();
    }
    public async Task<int> CreateShiftAsync(ShiftRequest request)
    {
        await access.StaffBranchAsync(request.BranchId);
        if (request.StartsAtUtc.Kind != DateTimeKind.Utc || request.EndsAtUtc.Kind != DateTimeKind.Utc || request.EndsAtUtc <= request.StartsAtUtc || request.EndsAtUtc - request.StartsAtUtc > TimeSpan.FromHours(24))
            throw new BadRequestException("Gửi thời gian UTC; ca trực phải từ hơn 0 đến 24 giờ.");
        var note = OperationInput.Text(request.Note, 500, 0);
        var id = 0;
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockPointsUserAsync(request.StaffId);
            var staff = (await uow.GetRepository<User>().GetByConditionAsync(u => u.Id == request.StaffId)).SingleOrDefault();
            if (staff is null || staff.Status != UserStatus.Active || staff.DeletedAt.HasValue || staff.Role != Role.Manager || staff.BranchId != request.BranchId)
                throw new ConflictException("Chọn nhân viên đang hoạt động thuộc cơ sở này.");
            var repo = uow.GetRepository<StaffShift>();
            if (await repo.CountByConditionAsync(s => s.DeletedAt == null && s.StaffId == staff.Id && s.StartsAtUtc < request.EndsAtUtc && request.StartsAtUtc < s.EndsAtUtc) > 0)
                throw new ConflictException("Ca trực giao với ca đã có.");
            var row = new StaffShift { BranchId = request.BranchId, StaffId = staff.Id, StartsAtUtc = request.StartsAtUtc, EndsAtUtc = request.EndsAtUtc, Note = note };
            await repo.AddAsync(row);
            await uow.SaveChangesAsync();
            id = row.Id;
            await audit.LogAsync(access.ActorId, "staff.shift-created", "StaffShift", id.ToString(), null, request);
        });
        return id;
    }
    public Task DeleteShiftAsync(int id) => uow.RunInTransactionAsync(async () =>
    {
        var repo = uow.GetRepository<StaffShift>();
        var row = await repo.GetByIdAsync(id) ?? throw new NotFoundException("StaffShift", id);
        await access.StaffBranchAsync(row.BranchId);
        await uow.LockPointsUserAsync(row.StaffId);
        row.DeletedAt = DateTime.UtcNow;
        repo.UpdateProperties(row, nameof(StaffShift.DeletedAt));
        await uow.SaveChangesAsync();
        await audit.LogAsync(access.ActorId, "staff.shift-deleted", "StaffShift", id.ToString(), null, null);
    });
    public async Task<IReadOnlyList<HandoverView>> HandoverAsync(int? branchId, int page)
    {
        var branch = await access.StaffBranchAsync(branchId);
        var (rows, _) = await uow.GetRepository<HandoverItem>().GetPagedAsync(h => branch == null || h.BranchId == branch, h => h.CreatedAt, OrderDirection.Desc, new Page(page, 30));
        return rows.Select(h => new HandoverView(h.Id, h.BranchId, h.Text, h.CreatedBy, h.CreatedAt, h.ClosedAt, h.Resolution)).ToArray();
    }
    public async Task<int> CreateHandoverAsync(HandoverRequest request)
    {
        await access.StaffBranchAsync(request.BranchId);
        var text = OperationInput.Text(request.Text);
        var id = 0;
        await uow.RunInTransactionAsync(async () =>
        {
            var row = new HandoverItem { BranchId = request.BranchId, CreatedBy = access.ActorId, Text = text };
            await uow.GetRepository<HandoverItem>().AddAsync(row);
            await uow.SaveChangesAsync();
            id = row.Id;
            await audit.LogAsync(access.ActorId, "staff.handover-created", "HandoverItem", id.ToString(), null, null);
        });
        return id;
    }
    public Task CloseHandoverAsync(int id, ContactOutcomeRequest request) => uow.RunInTransactionAsync(async () =>
    {
        var text = OperationInput.Text(request.Outcome, 1500);
        await uow.LockBookingRequestAsync($"handover:{id}");
        var repo = uow.GetRepository<HandoverItem>();
        var row = await repo.GetByIdAsync(id) ?? throw new NotFoundException("HandoverItem", id);
        await access.StaffBranchAsync(row.BranchId);
        if (row.ClosedAt.HasValue)
            throw new ConflictException("Việc bàn giao đã hoàn tất.");
        row.ClosedAt = DateTime.UtcNow;
        row.ClosedBy = access.ActorId;
        row.Resolution = text;
        repo.UpdateProperties(row, nameof(HandoverItem.ClosedAt), nameof(HandoverItem.ClosedBy), nameof(HandoverItem.Resolution));
        await uow.SaveChangesAsync();
        await audit.LogAsync(access.ActorId, "staff.handover-closed", "HandoverItem", id.ToString(), null, null, text);
    });
}
