using Badminton.Core.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Contract.Services.Audit
{
    public interface IAuditService
    {
        Task LogAsync(int actorId, string action, string entityType,
            string? entityId, object? before, object? after, string? reason = null);

        Task<IReadOnlyList<AuditEntry>> QueryAsync(
            string? entityType = null, string? entityId = null,
            DateOnly? from = null, DateOnly? to = null, int limit = 100);

        Task<(IReadOnlyList<AuditEntry> Items, int TotalCount)> QueryPagedAsync(
            string? entityType = null, string? entityId = null,
            DateOnly? from = null, DateOnly? to = null,
            Contract.Repositories.UnitOfWork.Page? page = null);
    }

    public sealed record AuditEntry(long Id, int? ActorId, string? ActorName,
        string Action, string EntityType, string? EntityId,
        string? BeforeJson, string? AfterJson, string? Reason, DateTime CreatedAt);

    public static class AuditJson
    {
        private static readonly JsonSerializerOptions Opts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        public static string? Serialize(object? value) =>
            value is null ? null : JsonSerializer.Serialize(value, Opts);
    }
}
