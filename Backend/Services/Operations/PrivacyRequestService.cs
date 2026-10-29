using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class PrivacyRequestService(IUnitOfWork uow, IOperationsAccess access, IAuditService audit, IInboxService inbox) : IPrivacyRequestService
{
    public async Task<int> CreateAsync(PrivacyRequestDto request)
    {
        await access.ActorAsync();
        var detail = OperationInput.Text(request.Detail);
        var kind = OperationInput.EnumValue<PrivacyRequestKind>(request.Kind);
        var id = 0;
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockBookingRequestAsync($"privacy:{access.ActorId}");
            var repo = uow.GetRepository<PrivacyRequest>();
            if (await repo.CountByConditionAsync(r => r.UserId == access.ActorId && r.Status == CaseStatus.Open) > 0)
                throw new ConflictException("Bạn đã có yêu cầu đang chờ xử lý.");
            var row = new PrivacyRequest { UserId = access.ActorId, Kind = kind, Detail = detail };
            await repo.AddAsync(row);
            await uow.SaveChangesAsync();
            id = row.Id;
            await audit.LogAsync(access.ActorId, "privacy.request-created", "PrivacyRequest", id.ToString(), null, new
            {
                row.Kind
            });
        });
        return id;
    }
    public async Task<IReadOnlyList<PrivacyView>> ListAsync(int page, bool administration)
    {
        var actor = await access.ActorAsync();
        if (administration && actor.Role != Role.Admin)
            throw new ConflictException("Chỉ Admin được xử lý yêu cầu dữ liệu cá nhân.");
        var (rows, _) = await uow.GetRepository<PrivacyRequest>().GetPagedAsync(r => administration || r.UserId == actor.Id, r => r.CreatedAt, OrderDirection.Desc, new Page(page, 30));
        return rows.Select(r => new PrivacyView(r.Id, r.UserId, r.Kind.ToString(), r.Detail, r.Status.ToString(), r.Resolution, r.CreatedAt)).ToArray();
    }
    public async Task ResolveAsync(int id, ResolutionRequest request)
    {
        if ((await access.ActorAsync()).Role != Role.Admin)
            throw new ConflictException("Chỉ Admin được xử lý yêu cầu dữ liệu cá nhân.");
        var resolution = OperationInput.Text(request.Resolution);
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockBookingRequestAsync($"privacy-resolution:{id}");
            var repo = uow.GetRepository<PrivacyRequest>();
            var row = await repo.GetByIdAsync(id) ?? throw new NotFoundException("PrivacyRequest", id);
            row.Resolve(request.Accepted, resolution, access.ActorId);
            repo.Update(row);
            await uow.SaveChangesAsync();
            await audit.LogAsync(access.ActorId, "privacy.request-resolved", "PrivacyRequest", id.ToString(), null, new
            {
                row.Status
            }, "Đã xác minh và ghi kết quả xử lý; không tự xóa lịch sử giao dịch.");
            await inbox.PublishAsync(row.UserId, null, $"privacy-resolved:{id}", "Yêu cầu dữ liệu cá nhân đã được phản hồi", resolution);
        });
    }
}
