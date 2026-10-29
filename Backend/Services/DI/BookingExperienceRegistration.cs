using Badminton.Core.Time;
using Contract.Services.Operations;
using Microsoft.Extensions.DependencyInjection;
using Services.Operations;
namespace Services.DI;

public static class BookingExperienceRegistration
{
    public static IServiceCollection AddBookingExperience(this IServiceCollection services)
    {
        services.AddSingleton<IBusinessClock, BusinessClock>();
        services.AddScoped<IOperationsAccess, OperationsAccess>();
        services.AddScoped<IBookingTransitionGuard, BookingTransitionGuard>();
        services.AddScoped<Contract.Services.Booking.IBookingCancellationAuthorization, BookingCancellationAuthorization>();
        services.AddScoped<Contract.Services.Report.IInventoryHistoryService, InventoryHistoryService>();
        services.AddScoped<IBookingAmendmentService, BookingAmendmentService>();
        services.AddScoped<IBatchBookingService, BatchBookingService>();
        services.AddScoped<IFrontDeskBookingService, FrontDeskBookingService>();
        services.AddScoped<IBookingBatchCoordinator, BookingBatchCoordinator>();
        services.AddScoped<IBookingCalendarService, BookingCalendarService>();
        services.AddScoped<IBookingCaseService, BookingCaseService>();
        services.AddScoped<IInboxService, InboxService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<NotifyService>();
        services.AddScoped<IDurableNotificationQueue>(sp => sp.GetRequiredService<NotifyService>());
        services.AddScoped<IDeliveryOperationsService, DeliveryOperationsService>();
        services.AddScoped<IDisruptionService, DisruptionService>();
        services.AddScoped<IStaffWorkService, StaffWorkService>();
        services.AddScoped<ICourtRatingProjection, CourtRatingProjection>();
        services.AddScoped<IReviewCommunityService, ReviewCommunityService>();
        services.AddScoped<ICourtFavoriteService, CourtFavoriteService>();
        services.AddScoped<IGroupAttendanceService, GroupAttendanceService>();
        services.AddScoped<IPrivacyRequestService, PrivacyRequestService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IOperationsReportService, OperationsReportService>();
        services.AddScoped<IPlayerMatchingService, PlayerMatchingService>();
        services.AddScoped<IGroupExpenseService, GroupExpenseService>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<IClubMaintenanceService, ClubMaintenanceService>();
        services.AddScoped<ICoachCourtService, CoachCourtService>();
        services.AddScoped<ICoachingService, CoachingService>();
        services.AddScoped<ITournamentService, TournamentService>();
        services.AddScoped<ICalendarSubscriptionService, CalendarSubscriptionService>();
        return services;
    }
}
