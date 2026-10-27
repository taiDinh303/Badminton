using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Time;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class GroupExpenseService(IUnitOfWork uow, IOperationsAccess access, IBusinessClock clock) : IGroupExpenseService
{
    public async Task<IReadOnlyList<ExpenseBookingView>> BookingsAsync(int groupId, int page)
    {
        await access.ActorAsync();
        var group = await uow.GetRepository<PlayGroup>().GetByIdAsync(groupId);
        if (group is null || group.DeletedAt.HasValue || await uow.GetRepository<GroupMember>().CountByConditionAsync(x => x.GroupId == groupId && x.UserId == access.ActorId) == 0)
            throw new ConflictException("Chỉ thành viên hiện tại được xem đơn nhóm.");
        var (rows, _) = await uow.GetRepository<Booking>().GetPagedAsync(x => x.GroupId == groupId && x.DeletedAt == null, x => x.CreatedAt, OrderDirection.Desc, new(page, 30));
        return rows.Select(x => new ExpenseBookingView(x.Id, x.BookingCode, x.Date.ToString("yyyy-MM-dd"), x.Status.ToString(), x.PayableAmount)).ToArray();
    }
    private async Task<Booking> Booking(int id)
    {
        await access.ActorAsync();
        var b = (await uow.GetRepository<Booking>().GetByConditionAsync(x => x.Id == id)).SingleOrDefault() ?? throw new NotFoundException("Booking", id);
        var group = b.GroupId.HasValue ? await uow.GetRepository<PlayGroup>().GetByIdAsync(b.GroupId.Value) : null;
        if (b.DeletedAt.HasValue || group is null || group.DeletedAt.HasValue || await uow.GetRepository<GroupMember>().CountByConditionAsync(x => x.GroupId == b.GroupId && x.UserId == access.ActorId) == 0)
            throw new ConflictException("Chỉ thành viên hiện tại được xem chi phí nhóm.");
        return b;
    }
    public async Task<ExpenseView?> GetAsync(int bookingId)
    {
        var b = await Booking(bookingId);
        var expense = (await uow.GetRepository<GroupExpense>().GetByConditionAsync(x => x.BookingId == bookingId)).SingleOrDefault();
        if (expense is null)
            return null;
        var rows = await uow.GetRepository<GroupExpenseShare>().GetByConditionAsync(x => x.ExpenseId == expense.Id);
        var ids = rows.Select(x => x.UserId).ToArray();
        var names = (await uow.GetRepository<User>().GetByConditionAsync(x => ids.Contains(x.Id))).ToDictionary(x => x.Id, x => x.FullName);
        return new(expense.Id, bookingId, expense.Total, b.Status is BookingStatus.Cancelled or BookingStatus.Refunded, b.UserId, rows.OrderBy(x => x.UserId).Select(x => new ExpenseShareView(x.UserId, names.GetValueOrDefault(x.UserId) ?? "Thành viên cũ", x.Amount, x.PaidAt, x.AttendedMinutes)).ToArray(), expense.SplitMethod);
    }
    public Task CreateAsync(int bookingId, int[] members) => SplitAsync(bookingId, members, null);
    public Task CreateByAttendanceAsync(int bookingId, AttendanceSplitRequest request)
    {
        if (request.Members is not { Length: > 0 and <= 50 } || request.Members.Any(x => x is null) || request.Members.Select(x => x.UserId).Distinct().Count() != request.Members.Length)
            throw new BadRequestException("Chọn các thành viên khác nhau.");
        return SplitAsync(bookingId, request.Members.Select(x => x.UserId).ToArray(), request.Members.ToDictionary(x => x.UserId, x => x.Minutes));
    }
    private Task SplitAsync(int bookingId, int[] members, Dictionary<int, int>? minutes) => uow.RunInTransactionAsync(async () =>
    {
        var b = await Booking(bookingId);
        await uow.LockGroupAsync(b.GroupId!.Value);
        b = await Booking(bookingId);
        if (b.UserId != access.ActorId)
            throw new ConflictException("Chỉ người đặt sân được chia tiền.");
        if (b.Status is not (BookingStatus.Confirmed or BookingStatus.Completed))
            throw new ConflictException("Chỉ chia chi phí đơn đã xác nhận hoặc hoàn thành.");
        if (members is not { Length: > 0 and <= 50 } || members.Distinct().Count() != members.Length)
            throw new BadRequestException("Chọn từ 1 đến 50 thành viên khác nhau.");
        if (minutes is not null)
        {
            var slot = await uow.GetRepository<TimeSlot>().GetByIdAsync(b.SlotId) ?? throw new NotFoundException("TimeSlot", b.SlotId);
            var duration = (int)(slot.EndTime.ToTimeSpan() - slot.StartTime.ToTimeSpan()).TotalMinutes;
            if (minutes.Values.Any(x => x < 0 || x > duration) || minutes.Values.Sum() == 0)
                throw new BadRequestException("Phút tham dự từ 0 đến thời lượng slot, ít nhất một người tham dự.");
        }
        var actual = await uow.GetRepository<GroupMember>().CountByConditionAsync(x => x.GroupId == b.GroupId && members.Contains(x.UserId));
        if (actual != members.Length)
            throw new ConflictException("Chỉ chia tiền cho thành viên hiện tại.");
        var existing = (await uow.GetRepository<GroupExpense>().GetByConditionAsync(x => x.BookingId == bookingId)).SingleOrDefault();
        if (existing is not null)
        {
            var old = await uow.GetRepository<GroupExpenseShare>().GetByConditionAsync(x => x.ExpenseId == existing.Id);
            if (existing.SplitMethod == (minutes is null ? "Equal" : "Attendance") && old.Select(x => x.UserId).Order().SequenceEqual(members.Order()) && (minutes is null || old.All(x => x.AttendedMinutes == minutes[x.UserId])))
                return;
            throw new ConflictException("Đã chốt bảng chia tiền; không ghi đè lịch sử đóng góp.");
        }
        var expense = new GroupExpense { BookingId = bookingId, Total = b.PayableAmount, CreatedBy = access.ActorId, SplitMethod = minutes is null ? "Equal" : "Attendance" };
        await uow.GetRepository<GroupExpense>().AddAsync(expense);
        await uow.SaveChangesAsync();
        var sorted = members.Order().ToArray();
        var amounts = Badminton.Core.Policies.GroupCostAllocation.Allocate(expense.Total, minutes ?? members.ToDictionary(x => x, _ => 1));
        for (var i = 0; i < sorted.Length; i++)
            await uow.GetRepository<GroupExpenseShare>().AddAsync(new()
            {
                ExpenseId = expense.Id,
                UserId = sorted[i],
                Amount = amounts[sorted[i]],
                AttendedMinutes = minutes?.GetValueOrDefault(sorted[i])
            });
        await uow.SaveChangesAsync();
    });
    public Task RecordAsync(int bookingId, int userId, bool paid) => uow.RunInTransactionAsync(async () =>
    {
        var b = await Booking(bookingId);
        await uow.LockGroupAsync(b.GroupId!.Value);
        b = await Booking(bookingId);
        if (b.UserId != access.ActorId)
            throw new ConflictException("Chỉ người đặt sân được xác nhận đóng góp.");
        if (paid && b.Status is not (BookingStatus.Confirmed or BookingStatus.Completed))
            throw new ConflictException("Đơn đã hủy/hoàn hoặc chưa xác nhận; không thu thêm đóng góp.");
        var expense = (await uow.GetRepository<GroupExpense>().GetByConditionAsync(x => x.BookingId == bookingId)).SingleOrDefault() ?? throw new ConflictException("Chưa chia chi phí.");
        var repo = uow.GetRepository<GroupExpenseShare>();
        var share = (await repo.GetByConditionAsync(x => x.ExpenseId == expense.Id && x.UserId == userId)).SingleOrDefault() ?? throw new NotFoundException("Phần đóng góp", userId);
        share = await repo.GetFreshByIdAsync(share.Id) ?? throw new NotFoundException("Phần đóng góp", userId);
        if (share.PaidAt.HasValue == paid)
            return;
        share.PaidAt = paid ? clock.UtcNow : null;
        share.RecordedBy = access.ActorId;
        share.UpdatedAt = clock.UtcNow;
        repo.Update(share);
        await uow.GetRepository<BookingHistory>().AddAsync(new()
        {
            BookingId = bookingId,
            ActorId = access.ActorId,
            Event = "group-contribution",
            Detail = $"Thành viên {userId}: {(paid ? "đã đóng" : "bỏ xác nhận đóng")} {share.Amount}đ."
        });
        await uow.SaveChangesAsync();
    });
}
