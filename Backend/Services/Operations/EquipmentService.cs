using Badminton.Core.Base;
using Badminton.Core.Time;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class EquipmentService(IUnitOfWork uow, IOperationsAccess access, IBusinessClock clock) : IEquipmentService
{
    private async Task<int> Held(int id) => (await uow.GetRepository<EquipmentOrder>().GetByConditionAsync(x => x.ItemId == id && ((x.Status == "Reserved" && (x.PickupExpiresAtUtc ?? x.DueAtUtc) > clock.UtcNow) || (x.Rental && x.Status == "Issued")))).Sum(x => x.Quantity);
    public async Task<IReadOnlyList<EquipmentItemView>> ItemsAsync(int branchId)
    {
        var actor = await access.ActorAsync();
        var staff = actor.Role != Badminton.Core.Enums.Role.Customer && (actor.Role == Badminton.Core.Enums.Role.Admin || actor.BranchId == branchId);
        var items = await uow.GetRepository<EquipmentItem>().GetByConditionAsync(x => x.BranchId == branchId && (x.Active || staff));
        var result = new List<EquipmentItemView>();
        foreach (var x in items)
            result.Add(new(x.Id, x.BranchId, x.Name, x.Rental, x.Stock, x.Stock - await Held(x.Id), x.UnitPrice, x.Active));
        return result;
    }
    public async Task<int> SaveItemAsync(int? id, EquipmentItemRequest r)
    {
        await access.StaffBranchAsync(r.BranchId);
        await ActiveBranch(r.BranchId);
        ClubInput.Number(r.Stock, 0, 100000, "Tồn kho");
        ClubInput.Number(r.UnitPrice, 0, 10000000, "Giá đơn vị");
        var row = new EquipmentItem { BranchId = r.BranchId };
        await uow.RunInTransactionAsync(async () =>
        {
            if (id.HasValue)
            {
                await uow.LockNotificationAsync($"equipment:{id}");
                row = await Find(id.Value);
                if (row.BranchId != r.BranchId || row.Rental != r.Rental)
                    throw new ConflictException("Không đổi cơ sở hoặc loại dụng cụ đã tạo.");
                if (r.Stock < await Held(row.Id))
                    throw new ConflictException("Tồn kho thấp hơn số lượng đang giữ/cho thuê.");
            }
            row.Name = ClubInput.Text(r.Name, 120, "Tên dụng cụ");
            row.Rental = r.Rental;
            row.Stock = r.Stock;
            row.UnitPrice = r.UnitPrice;
            row.Active = r.Active;
            row.UpdatedAt = clock.UtcNow;
            if (id.HasValue)
                uow.GetRepository<EquipmentItem>().Update(row);
            else
                await uow.GetRepository<EquipmentItem>().AddAsync(row);
            await uow.SaveChangesAsync();
        });
        return row.Id;
    }
    public async Task<int> ReserveAsync(EquipmentOrderRequest r)
    {
        await access.ActorAsync();
        ClubInput.Number(r.Quantity, 1, 100, "Số lượng");
        var key = ClubInput.Text(r.RequestKey, 60, "Mã yêu cầu");
        if (r.DueAtUtc.Kind != DateTimeKind.Utc)
            throw new BadRequestException("Hẹn nhận/trả phải là giờ UTC.");
        var result = 0;
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockNotificationAsync($"equipment-request:{access.ActorId}:{key}");
            var repo = uow.GetRepository<EquipmentOrder>();
            var old = (await repo.GetByConditionAsync(x => x.UserId == access.ActorId && x.RequestKey == key)).SingleOrDefault();
            if (old is not null)
            {
                if (old.ItemId != r.ItemId || old.Quantity != r.Quantity || old.DueAtUtc != r.DueAtUtc)
                    throw new ConflictException("Mã yêu cầu đã dùng cho nội dung khác.");
                result = old.Id;
                return;
            }
            await uow.LockNotificationAsync($"equipment:{r.ItemId}");
            if (r.DueAtUtc <= clock.UtcNow || r.DueAtUtc > clock.UtcNow.AddDays(30))
                throw new BadRequestException("Hẹn nhận/trả trong 30 ngày tới (UTC).");
            var item = await Find(r.ItemId);
            await ActiveBranch(item.BranchId);
            if (!item.Active || item.Stock - await Held(item.Id) < r.Quantity)
                throw new ConflictException("Không đủ dụng cụ/cầu khả dụng.");
            if (await repo.CountByConditionAsync(x => x.UserId == access.ActorId && x.Status == "Reserved" && (x.PickupExpiresAtUtc ?? x.DueAtUtc) > clock.UtcNow) >= 20)
                throw new ConflictException("Tối đa 20 yêu cầu giữ hàng chưa nhận.");
            var row = new EquipmentOrder { ItemId = item.Id, UserId = access.ActorId, Quantity = r.Quantity, UnitPrice = item.UnitPrice, Rental = item.Rental, RequestKey = key, DueAtUtc = r.DueAtUtc, PickupExpiresAtUtc = r.DueAtUtc < clock.UtcNow.AddMinutes(30) ? r.DueAtUtc : clock.UtcNow.AddMinutes(30) };
            await repo.AddAsync(row);
            await uow.SaveChangesAsync();
            result = row.Id;
        });
        return result;
    }
    public async Task<IReadOnlyList<EquipmentOrderView>> OrdersAsync(bool staff, int? branchId, int page)
    {
        await access.ActorAsync();
        var branch = staff ? await access.StaffBranchAsync(branchId) : null;
        var items = (await uow.GetRepository<EquipmentItem>().GetByConditionAsync(x => !branch.HasValue || x.BranchId == branch)).ToDictionary(x => x.Id);
        var ids = items.Keys.ToArray();
        var (rows, _) = await uow.GetRepository<EquipmentOrder>().GetPagedAsync(x => ids.Contains(x.ItemId) && (staff || x.UserId == access.ActorId), x => x.CreatedAt, OrderDirection.Desc, new(page, 30));
        return rows.Select(x => new EquipmentOrderView(x.Id, x.UserId, items[x.ItemId].BranchId, items[x.ItemId].Name, x.Rental, x.Quantity, checked(x.Quantity * x.UnitPrice), x.Status == "Reserved" && (x.PickupExpiresAtUtc ?? x.DueAtUtc) <= clock.UtcNow ? "Expired" : x.Status, x.DueAtUtc, x.ReturnedAtUtc, x.Rental && x.Status == "Issued" && x.DueAtUtc <= clock.UtcNow)).ToArray();
    }
    public Task TransitionAsync(int id, string action) => uow.RunInTransactionAsync(async () =>
    {
        await access.ActorAsync();
        var repo = uow.GetRepository<EquipmentOrder>();
        var row = (await repo.GetByConditionAsync(x => x.Id == id)).SingleOrDefault() ?? throw new NotFoundException("Yêu cầu dụng cụ", id);
        await uow.LockNotificationAsync($"equipment:{row.ItemId}");
        row = await repo.GetFreshByIdAsync(id) ?? throw new NotFoundException("Yêu cầu dụng cụ", id);
        var item = await Find(row.ItemId);
        if (action == "cancel")
        {
            if (row.UserId != access.ActorId)
                await access.StaffBranchAsync(item.BranchId);
            if (row.Status == "Cancelled")
                return;
            if (row.Status != "Reserved")
                throw new ConflictException("Chỉ hủy hàng chưa giao.");
            row.Status = "Cancelled";
        }
        else
        {
            await access.StaffBranchAsync(item.BranchId);
            if (action == "issue")
            {
                var target = row.Rental ? "Issued" : "Sold";
                if (row.Status == target)
                    return;
                if (row.Status != "Reserved")
                    throw new ConflictException("Yêu cầu không còn chờ giao.");
                if ((row.PickupExpiresAtUtc ?? row.DueAtUtc) <= clock.UtcNow)
                    throw new ConflictException("Yêu cầu đã hết 30 phút giữ hàng hoặc quá hẹn nhận; khách cần đặt lại.");
                if (!row.Rental)
                {
                    item.Stock -= row.Quantity;
                    uow.GetRepository<EquipmentItem>().Update(item);
                }
                row.Status = target;
            }
            else if (action == "return")
            {
                if (row.Status == "Returned")
                    return;
                if (!row.Rental || row.Status != "Issued")
                    throw new ConflictException("Chỉ nhận lại dụng cụ đã cho thuê.");
                row.Status = "Returned";
                row.ReturnedAtUtc = clock.UtcNow;
            }
            else
                throw new BadRequestException("Thao tác dụng cụ không hợp lệ.");
            row.HandledBy = access.ActorId;
        }
        row.UpdatedAt = clock.UtcNow;
        repo.Update(row);
        await uow.SaveChangesAsync();
    });
    public async Task<EquipmentConditionView?> ConditionAsync(int id)
    {
        await access.ActorAsync();
        var order = await uow.GetRepository<EquipmentOrder>().GetByIdAsync(id) ?? throw new NotFoundException("Yêu cầu", id);
        var item = await Find(order.ItemId);
        if (order.UserId != access.ActorId)
            await access.StaffBranchAsync(item.BranchId);
        var row = (await uow.GetRepository<EquipmentConditionReport>().GetByConditionAsync(x => x.OrderId == id)).SingleOrDefault();
        return row is null ? null : new(row.OrderId, row.ActorId, row.UnusableQuantity, row.Lost, row.AssessedAmount, row.Reason, row.CreatedAt);
    }
    public Task RecordConditionAsync(int id, EquipmentConditionRequest r) => uow.RunInTransactionAsync(async () =>
    {
        var candidate = await uow.GetRepository<EquipmentOrder>().GetByIdAsync(id) ?? throw new NotFoundException("Yêu cầu", id);
        await uow.LockNotificationAsync($"equipment:{candidate.ItemId}");
        var row = (await uow.GetRepository<EquipmentOrder>().GetFreshByIdAsync(id))!;
        var item = await Find(row.ItemId);
        await access.StaffBranchAsync(item.BranchId);
        var reason = ClubInput.Text(r.Reason, 1000, "Biên bản hư/mất");
        ClubInput.Number(r.UnusableQuantity, 1, row.Quantity, "Số dụng cụ không thể dùng");
        ClubInput.Number(r.AssessedAmount, 0, 100000000, "Khoản đề nghị đối chiếu");
        var repo = uow.GetRepository<EquipmentConditionReport>();
        var old = (await repo.GetByConditionAsync(x => x.OrderId == id)).SingleOrDefault();
        if (old is not null)
        {
            if (old.UnusableQuantity == r.UnusableQuantity && old.Lost == r.Lost && old.AssessedAmount == r.AssessedAmount && old.Reason == reason)
                return;
            throw new ConflictException("Biên bản đã chốt; không ghi đè lịch sử.");
        }
        if (!row.Rental || row.Status != "Issued")
            throw new ConflictException("Chỉ lập biên bản cho dụng cụ đang cho thuê.");
        item.Stock -= r.UnusableQuantity;
        uow.GetRepository<EquipmentItem>().Update(item);
        row.Status = r.Lost ? "Lost" : "Damaged";
        row.ReturnedAtUtc = clock.UtcNow;
        row.UpdatedAt = clock.UtcNow;
        row.HandledBy = access.ActorId;
        uow.GetRepository<EquipmentOrder>().Update(row);
        await repo.AddAsync(new()
        {
            OrderId = id,
            ActorId = access.ActorId,
            UnusableQuantity = r.UnusableQuantity,
            Lost = r.Lost,
            AssessedAmount = r.AssessedAmount,
            Reason = reason,
            CreatedAt = clock.UtcNow
        });
        await uow.SaveChangesAsync();
    });
    private async Task<EquipmentItem> Find(int id) => await uow.GetRepository<EquipmentItem>().GetFreshByIdAsync(id) ?? throw new NotFoundException("Dụng cụ", id);
    private async Task ActiveBranch(int id)
    {
        var branch = await uow.GetRepository<Branch>().GetByIdAsync(id);
        if (branch is null || branch.DeletedAt.HasValue || branch.Status != Badminton.Core.Enums.BranchStatus.Active)
            throw new ConflictException("Cơ sở không hoạt động.");
    }
}
