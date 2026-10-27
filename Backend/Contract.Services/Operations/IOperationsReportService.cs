using ModelViews.Operations;
namespace Contract.Services.Operations;

public interface IOperationsReportService
{
    Task<string> BookingCsvAsync(DateOnly from, DateOnly to, int? branchId);
    Task<string> AuditCsvAsync(DateOnly from, DateOnly to);
    Task<CustomerInsights> InsightsAsync(DateOnly from, DateOnly to, int? branchId);
}
