using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Maintenance;
using M.Core.Base;
using M.Core.Enums;
using Microsoft.AspNetCore.Http;

namespace Services
{
    public sealed class MaintenanceService : BaseService, IMaintenanceService
    {
        private readonly IAuditService _audit;
        public MaintenanceService(IUnitOfWork uow, IHttpContextAccessor http, IAuditService audit)
            : base(uow, http) { _audit = audit; }

        public async Task SetAsync(int courtId, DateOnly date, CourtDateStatus status,
            int actorId, string? note = null)
        {
            var court = await Repository<Court>().GetByIdAsync(courtId);
            if (court is null || !court.IsActive)
                throw new NotFoundException("Court", courtId);

            var repo = Repository<CourtDateStatusEntry>();
            var existing = (await repo.GetByConditionAsync(
                r => r.CourtId == courtId && r.Date == date)).FirstOrDefault();

            var row = existing ?? new CourtDateStatusEntry
            {
                CourtId = courtId,
                Date = date
            };

            var before = new { row.Status, row.Note };
            row.Status = status;
            row.Note = note;
            row.CreatedById = actorId;
            row.UpdatedAt = DateTime.UtcNow;

            if (existing is null)
                _ = repo.AddAsync(row);
            else
                repo.Update(row);

            await SaveAsync();
            await _audit.LogAsync(actorId,
                status == CourtDateStatus.Open ? "maintenance.open" : "maintenance.lock",
                "CourtDateStatus", $"{courtId}@{date:yyyy-MM-dd}", before,
                new { row.Status, row.Note, row.CreatedById }, note);
        }

        public async Task<CourtDateStatus> GetStatusAsync(int courtId, DateOnly date)
        {
            var rows = await Repository<CourtDateStatusEntry>()
                .GetByConditionAsync(r => r.CourtId == courtId && r.Date == date);
            return rows.FirstOrDefault()?.Status ?? CourtDateStatus.Open;
        }
    }
}
