using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using M.Core.Base;
using Microsoft.AspNetCore.Http;

namespace Services
{
    public sealed class AuditService : BaseService, IAuditService
    {
        public AuditService(IUnitOfWork uow, IHttpContextAccessor http) : base(uow, http) { }

        public async Task LogAsync(int actorId, string action, string entityType,
            string? entityId, object? before, object? after, string? reason = null)
        {
            _ = Repository<AuditLog>().AddAsync(new AuditLog
            {
                ActorId = actorId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                BeforeJson = AuditJson.Serialize(before),
                AfterJson = AuditJson.Serialize(after),
                Reason = reason
            });
            await SaveAsync();
        }

        public async Task<IReadOnlyList<AuditEntry>> QueryAsync(
            string? entityType = null, string? entityId = null,
            DateOnly? from = null, DateOnly? to = null, int limit = 100)
        {
            var logs = await Repository<AuditLog>().GetAllAsync();
            return logs
                .Where(a => entityType is null || a.EntityType == entityType)
                .Where(a => entityId is null || a.EntityId == entityId)
                .Where(a => from is null || a.CreatedAt.Date >= from.Value.ToDateTime(TimeOnly.MinValue))
                .Where(a => to is null || a.CreatedAt.Date <= to.Value.ToDateTime(TimeOnly.MinValue))
                .OrderByDescending(a => a.CreatedAt)
                .Take(limit)
                .Select(a => new AuditEntry(a.Id, a.ActorId, a.Actor?.FullName,
                    a.Action, a.EntityType, a.EntityId, a.BeforeJson, a.AfterJson,
                    a.Reason, a.CreatedAt))
                .ToList();
        }
    }
}
