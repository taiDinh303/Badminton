using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Time;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Operations;
using Contract.Services.Pricing;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class BookingAmendmentService(IUnitOfWork uow, IOperationsAccess access, IBusinessClock clock,
    BookingAvailabilityService availability, IPricingService pricing, IAuditService audit, IInboxService inbox,
    IBookingTransitionGuard transitions) : IBookingAmendmentService
{
    public async Task RescheduleAsync(int id, RescheduleRequest request)
    {
        var reason = OperationInput.Text(request.Reason, 500);
        await uow.RunInTransactionAsync(async () =>
        {
            await transitions.AcquireAsync(id);
            await transitions.EnsureNoPendingRefundAsync(id);
            var booking = await access.BookingAsync(id);
            var actor = await access.ActorAsync();
            var targetCourt = await uow.GetRepository<Court>().GetByIdAsync(request.CourtId) ?? throw new NotFoundException("Court", request.CourtId);
            if (actor.Role != Role.Customer)
                await access.StaffBranchAsync(targetCourt.BranchId);
            if (booking.CourtId == request.CourtId && booking.SlotId == request.SlotId && booking.Date == request.Date)
                throw new BadRequestException("Lịch mới trùng lịch hiện tại.");
            foreach (var branch in new[] { booking.BranchId, targetCourt.BranchId }.Distinct().Order())
                await uow.LockBookingRequestAsync($"branch-config:{branch}");
            if (booking.GroupId is { } group)
                await uow.LockGroupAsync(group);
            foreach (var schedule in new[] { (booking.CourtId, booking.Date), (request.CourtId, request.Date) }.Distinct().OrderBy(x => x.Item1).ThenBy(x => x.Item2))
                await uow.LockCourtDateAsync(schedule.Item1, schedule.Item2);
            await uow.LockPointsUserAsync(booking.UserId);
            var owner = (await uow.GetRepository<User>().GetByConditionAsync(u => u.Id == booking.UserId)).Single();
            if (!owner.IsVerified || owner.DeletedAt.HasValue || owner.Status != UserStatus.Active || (owner.NoShowLimited && owner.NoShowLimitedPeriod == new DateOnly(clock.Today.Year, clock.Today.Month, 1)))
                throw new ConflictException("Tài khoản khách không đủ điều kiện đổi lịch.");
            if (request.Date > clock.Today.AddDays(30))
                throw new BadRequestException("Chọn lịch trong 30 ngày tới.");
            var available = (await availability.GetAsync(targetCourt.BranchId, request.Date)).SingleOrDefault(c => c.CourtId == request.CourtId);
            if (available?.Slots.Any(s => s.SlotId == request.SlotId && !s.IsLocked) != true)
                throw new ConflictException("Lịch mới không còn trống hoặc không hợp lệ.");
            var court = (await uow.GetRepository<Court>().GetByConditionAsync(c => c.Id == request.CourtId)).Single();
            var slot = (await uow.GetRepository<TimeSlot>().GetByConditionAsync(s => s.Id == request.SlotId)).Single();
            if (BookingPricing.SlotTotal(court, slot, request.Date, booking.PlayerCount, pricing) != booking.TotalAmount)
                throw new ConflictException("Chỉ đổi sang lịch cùng giá gốc; giữ nguyên ưu đãi và số tiền của đơn.");
            var groupId = booking.GroupId;
            var count = await uow.GetRepository<Booking>().CountByConditionAsync(b => b.Id != id && b.Date == request.Date
                && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                && (groupId != null ? b.GroupId == groupId : b.GroupId == null && b.UserId == booking.UserId));
            if (count >= (groupId.HasValue ? 8 : 4))
                throw new ConflictException("Đã đạt giới hạn đặt sân trong ngày.");
            var before = new
            {
                booking.CourtId,
                booking.SlotId,
                booking.Date
            };
            var changed = await uow.Bookings.RescheduleAsync(id, booking.CourtId, booking.SlotId, booking.Date,
                request.CourtId, request.SlotId, request.Date, court.BranchId, clock.UtcNow, clock.LocalNow);
            if (changed != 1)
                throw new ConflictException("Đơn đã đổi trạng thái, hết hạn hoặc đã check-in. Hãy tải lại.");
            var history = new BookingHistory
            {
                BookingId = id,
                ActorId = access.ActorId,
                Event = "rescheduled",
                Detail = $"Đổi từ sân {booking.CourtId}, {booking.Date:yyyy-MM-dd}, slot {booking.SlotId} sang sân {court.Id}, {request.Date:yyyy-MM-dd}, slot {slot.Id}. Lý do: {reason}"
            };
            await uow.GetRepository<BookingHistory>().AddAsync(history);
            await uow.SaveChangesAsync();
            await audit.LogAsync(access.ActorId, "booking.reschedule", "Booking", id.ToString(), before, request, reason);
            await inbox.PublishAsync(booking.UserId, id, $"rescheduled:{history.Id}", "Đã đổi lịch đặt sân", history.Detail);
        });
    }
}
