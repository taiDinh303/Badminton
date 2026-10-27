namespace Contract.Services.Operations;

/// <summary>Coordinates booking changes with durable refund intents in the current transaction.</summary>
public interface IBookingTransitionGuard
{
    Task AcquireAsync(int bookingId);
    Task<bool> HasPendingRefundAsync(int bookingId);
    Task EnsureNoPendingRefundAsync(int bookingId);
    Task EnsureMaintenanceScopeAsync(Contract.Repositories.Entity.Booking booking, Contract.Services.Maintenance.MaintenanceBookingScope scope);
}
