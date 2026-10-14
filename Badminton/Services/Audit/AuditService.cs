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

        public async Task<(IReadOnlyList<AuditEntry> Items, int TotalCount)> QueryPagedAsync(
            string? entityType = null, string? entityId = null,
            DateOnly? from = null, DateOnly? to = null, Page? page = null)
        {
            // Push every filter down to SQL (the old code loaded the whole
            // AuditLog table and filtered/sliced in memory — O(table) I/O on
            // every call).
            var fromUtc = from?.ToDateTime(TimeOnly.MinValue);
            var toUtc = to?.ToDateTime(TimeOnly.MaxValue);
            var et = entityType; var eid = entityId;
            System.Linq.Expressions.Expression<Func<AuditLog, bool>> predicate =
                a => (et == null || a.EntityType == et)
                && (eid == null || a.EntityId == eid)
                && (fromUtc == null || a.CreatedAt >= fromUtc)
                && (toUtc == null || a.CreatedAt <= toUtc);

            var pg = page ?? new Page(1, 100);
            var (logs, total) = await Repository<AuditLog>().GetPagedAsync(
                predicate, a => a.CreatedAt, OrderDirection.Desc, pg);

            var items = logs
                .Select(a => new AuditEntry(a.Id, a.ActorId, a.Actor?.FullName,
                    a.Action, a.EntityType, a.EntityId, a.BeforeJson, a.AfterJson,
                    a.Reason, a.CreatedAt))
                .ToList();
            return (items, total);
        }

        /// <summary>Backward-compatible capped list (first page only).</summary>
        public async Task<IReadOnlyList<AuditEntry>> QueryAsync(
            string? entityType = null, string? entityId = null,
            DateOnly? from = null, DateOnly? to = null, int limit = 100)
        {
            var (items, _) = await QueryPagedAsync(entityType, entityId, from, to,
                new Page(1, Math.Max(1, limit)));
            return items;
        }
    }
}
