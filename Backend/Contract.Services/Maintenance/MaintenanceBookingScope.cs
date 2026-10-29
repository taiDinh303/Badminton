namespace Contract.Services.Maintenance;

public sealed record MaintenanceBookingScope(int CourtId, DateOnly Date, int SlotId);
