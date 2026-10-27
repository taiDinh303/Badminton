using System.Text;
using Badminton.Core.Base;
using Badminton.Core.Enums;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class OperationsReportService(IUnitOfWork uow, IOperationsAccess access, IOperationsReadStore reads) : IOperationsReportService
{
    public async Task<string> BookingCsvAsync(DateOnly from, DateOnly to, int? branchId)
    {
        var rows = await BookingsAsync(from, to, branchId);
        var csv = new StringBuilder("Code,Date,CourtId,SlotId,Status,Players,CheckedIn\r\n");
        foreach (var b in rows)
            csv.AppendLine(string.Join(',', new[] { Cell(b.BookingCode), Cell(b.Date.ToString("yyyy-MM-dd")), Cell(b.CourtId.ToString()), Cell(b.SlotId.ToString()), Cell(b.Status.ToString()), Cell(b.PlayerCount.ToString()), Cell(b.CheckedInAt.HasValue ? "Yes" : "No") }));
        return csv.ToString();
    }
    public async Task<string> AuditCsvAsync(DateOnly from, DateOnly to)
    {
        if ((await access.ActorAsync()).Role != Role.Admin)
            throw new ConflictException("Chỉ Admin được xuất nhật ký.");
        OperationInput.DateRange(from, to, 365);
        var start = DateTime.SpecifyKind(from.ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7), DateTimeKind.Utc);
        var (rows, count) = await uow.GetRepository<AuditLog>().GetPagedAsync(a => a.CreatedAt >= start && a.CreatedAt < end, a => a.CreatedAt, OrderDirection.Asc, new Page(1, 10000));
        if (count > 10000)
            throw new BadRequestException("Thu hẹp khoảng xuất dưới 10.000 dòng.");
        var csv = new StringBuilder("AtUtc,ActorId,Action,Entity,EntityId,Reason\r\n");
        foreach (var row in rows)
            csv.AppendLine(string.Join(',', new[] { Cell(row.CreatedAt.ToString("O")), Cell(row.ActorId?.ToString() ?? ""), Cell(row.Action), Cell(row.EntityType), Cell(row.EntityId ?? ""), Cell(row.Reason ?? "") }));
        return csv.ToString();
    }
    public async Task<CustomerInsights> InsightsAsync(DateOnly from, DateOnly to, int? branchId)
    {
        var branch = await access.StaffBranchAsync(branchId);
        var rows = await BookingsAsync(from, to, branch);
        var ids = rows.Select(b => b.UserId).Distinct().ToArray();
        var prior = await reads.ReturningCustomersAsync(ids, from, branch);
        var noShows = rows.Count(b => b.Status == BookingStatus.NoShow);
        var courtIds = (await uow.GetRepository<Court>().GetByConditionAsync(c => branch == null || c.BranchId == branch)).Select(c => c.Id).ToArray();
        var waiting = await uow.GetRepository<WaitlistEntry>().CountByConditionAsync(w => courtIds.Contains(w.CourtId) && w.Date >= from && w.Date <= to);
        var promoted = await uow.GetRepository<WaitlistEntry>().CountByConditionAsync(w => courtIds.Contains(w.CourtId) && w.Date >= from && w.Date <= to && w.Status == WaitlistStatus.Promoted);
        return new(ids.Length, ids.Length - prior, prior, rows.Count, noShows, rows.Count == 0 ? 0 : Math.Round(100m * noShows / rows.Count, 2), waiting, promoted);
    }
    private async Task<IReadOnlyList<Booking>> BookingsAsync(DateOnly from, DateOnly to, int? branchId)
    {
        OperationInput.DateRange(from, to, 365);
        var branch = await access.StaffBranchAsync(branchId);
        var (rows, count) = await uow.GetRepository<Booking>().GetPagedAsync(b => b.DeletedAt == null && b.Date >= from && b.Date <= to && (branch == null || b.BranchId == branch), b => b.CreatedAt, OrderDirection.Asc, new Page(1, 10000));
        if (count > 10000)
            throw new BadRequestException("Thu hẹp khoảng báo cáo dưới 10.000 đơn.");
        return rows;
    }
    private static string Cell(string value)
    {
        value = value.Replace("\r", " ").Replace("\n", " ");
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@') || value.StartsWith('\t'))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
