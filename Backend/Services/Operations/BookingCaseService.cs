using Badminton.Core.Base;
using Badminton.Core.Enums;
using Badminton.Core.Time;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Operations;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class BookingCaseService(IUnitOfWork uow, IOperationsAccess access, IBusinessClock clock,
    BookingCompletionService completion, IAuditService audit, IInboxService inbox, IOperationsReadStore reads,
    IBookingTransitionGuard transitions) : IBookingCaseService
{
    public async Task<int> CreateAsync(int bookingId, BookingCaseRequest request)
    {
        var kind = OperationInput.EnumValue<BookingCaseKind>(request.Kind);
        var reason = OperationInput.Text(request.Reason);
        var result = 0;
        await uow.RunInTransactionAsync(async () =>
        {
            await uow.LockBookingRequestAsync($"case:{bookingId}");
            var booking = await access.BookingAsync(bookingId, false);
            if (kind == BookingCaseKind.NoShow && booking.Status != BookingStatus.NoShow)
                throw new ConflictException("Đơn chưa được ghi nhận no-show.");
            var repo = uow.GetRepository<BookingCase>();
            if (await repo.CountByConditionAsync(c => c.BookingId == bookingId && c.Status == CaseStatus.Open) > 0)
                throw new ConflictException("Đơn đã có yêu cầu đang chờ xử lý.");
            var row = new BookingCase { BookingId = bookingId, UserId = access.ActorId, Kind = kind, Reason = reason };
            await repo.AddAsync(row);
            await uow.SaveChangesAsync();
            result = row.Id;
            await audit.LogAsync(access.ActorId, "booking.case-opened", "BookingCase", row.Id.ToString(), null, new
            {
                row.BookingId,
                row.Kind
            });
        });
        return result;
    }
    public async Task<IReadOnlyList<CaseView>> ListAsync(int? bookingId, int? branchId, int page)
    {
        var actor = await access.ActorAsync();
        var branch = actor.Role == Role.Customer ? null : await access.StaffBranchAsync(branchId);
        var rows = await reads.CasesAsync(actor.Role == Role.Customer ? actor.Id : null, branch, bookingId, new Page(page, 30));
        return rows.Select(c => new CaseView(c.Id, c.BookingId, c.Kind.ToString(), c.Reason, c.Status.ToString(), c.Resolution, c.CreatedAt)).ToArray();
    }
    public async Task ResolveAsync(int id, ResolutionRequest request)
    {
        var text = OperationInput.Text(request.Resolution);
        await uow.RunInTransactionAsync(async () =>
        {
            var repo = uow.GetRepository<BookingCase>();
            var candidate = (await repo.GetByConditionAsync(c => c.Id == id)).SingleOrDefault() ?? throw new NotFoundException("BookingCase", id);
            await uow.LockBookingRequestAsync($"case:{candidate.BookingId}");
            var row = (await repo.GetByIdAsync(id))!;
            await transitions.AcquireAsync(row.BookingId);
            await transitions.EnsureNoPendingRefundAsync(row.BookingId);
            var booking = await access.StaffBookingAsync(row.BookingId);
            row.Resolve(request.Accepted, text, access.ActorId);
            if (request.Accepted && row.Kind == BookingCaseKind.NoShow)
                await CorrectNoShowAsync(booking);
            repo.UpdateProperties(row, nameof(BookingCase.Status), nameof(BookingCase.Resolution), nameof(BookingCase.ResolvedBy), nameof(BookingCase.UpdatedAt));
            await uow.SaveChangesAsync();
            await audit.LogAsync(access.ActorId, "booking.case-resolved", "BookingCase", id.ToString(), null, new
            {
                row.Status
            }, text);
            await inbox.PublishAsync(row.UserId, row.BookingId, $"case-resolved:{id}", "Yêu cầu về đơn đã được xử lý", text);
        });
    }
    private async Task CorrectNoShowAsync(Booking booking)
    {
        var slot = await uow.GetRepository<TimeSlot>().GetByIdAsync(booking.SlotId) ?? throw new NotFoundException("TimeSlot", booking.SlotId);
        if (booking.Status != BookingStatus.NoShow || HanoiTime.SlotEnd(booking.Date, slot.EndTime) > clock.LocalNow)
            throw new ConflictException("Chỉ đính chính no-show sau khi hết giờ và đã xác minh khách thực sự đến sân.");
        await uow.LockCourtDateAsync(booking.CourtId, booking.Date);
        await uow.LockPointsUserAsync(booking.UserId);
        var overlaps = await uow.GetRepository<Booking>().CountByConditionAsync(b => b.Id != booking.Id && b.CourtId == booking.CourtId && b.Date == booking.Date && b.SlotId == booking.SlotId && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed));
        if (overlaps != 0)
            throw new ConflictException("Lịch cũ có đơn hoạt động khác; cần đối soát thủ công trước.");
        var user = await uow.GetRepository<User>().GetByIdAsync(booking.UserId) ?? throw new NotFoundException("User", booking.UserId);
        if (booking.CancelledAt is { } marked && user.NoShowPeriod == DateOnly.FromDateTime(marked.AddHours(7)).MonthStart())
        {
            user.NoShowCount = Math.Max(0, user.NoShowCount - 1);
            if (user.NoShowCount < 3 && user.NoShowLimitedPeriod == user.NoShowPeriod.AddMonths(1))
            {
                user.NoShowLimited = false;
                user.NoShowLimitedPeriod = null;
            }
            uow.GetRepository<User>().UpdateProperties(user, nameof(User.NoShowCount), nameof(User.NoShowLimited), nameof(User.NoShowLimitedPeriod));
        }
        booking.Status = BookingStatus.Confirmed;
        booking.CheckedInAt = clock.UtcNow;
        booking.CancelledAt = null;
        booking.CancelReason = null;
        uow.GetRepository<Booking>().UpdateProperties(booking, nameof(Booking.Status), nameof(Booking.CheckedInAt), nameof(Booking.CancelledAt), nameof(Booking.CancelReason));
        await uow.SaveChangesAsync();
        await completion.CompleteAsync(booking.Id);
        await uow.GetRepository<BookingHistory>().AddAsync(new()
        {
            BookingId = booking.Id,
            ActorId = access.ActorId,
            Event = "attendance-corrected",
            Detail = "Nhân viên đã xác minh khách đến sân, đính chính no-show và hoàn thành đơn."
        });
    }
}
