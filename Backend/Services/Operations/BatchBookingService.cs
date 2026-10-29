using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Badminton.Core.Base;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Booking;
using Contract.Services.Operations;
using ModelViews.Booking;
using ModelViews.Operations;
namespace Services.Operations;

public sealed class BatchBookingService(IUnitOfWork uow, IOperationsAccess access, IBookingBatchCoordinator coordinator) : IBatchBookingService
{
    public async Task<IReadOnlyList<BookingView>> CreateAsync(BatchBookingRequest request)
    {
        await access.ActorAsync();
        if (request.Weeks is < 1 or > 5 || request.SlotIds is not { Length: > 0 and <= 4 }
            || request.SlotIds.Distinct().Count() != request.SlotIds.Length)
            throw new BadRequestException("Chọn 1–4 khung giờ khác nhau và 1–5 tuần trong giới hạn 30 ngày.");
        var slots = (await uow.GetRepository<TimeSlot>().GetByConditionAsync(s => request.SlotIds.Contains(s.Id))).OrderBy(s => s.StartTime).ToList();
        if (slots.Count != request.SlotIds.Length)
            throw new BadRequestException("Khung giờ không tồn tại.");
        for (var i = 1; i < slots.Count; i++)
            if (slots[i - 1].EndTime != slots[i].StartTime)
                throw new BadRequestException("Các khung giờ phải liên tiếp.");
        var requests = Enumerable.Range(0, request.Weeks).SelectMany(week => slots.Select(slot =>
            new CreateBookingRequest(request.CourtId, request.FirstDate.AddDays(week * 7).ToString("yyyy-MM-dd"), slot.Id,
                request.PlayerCount, request.GroupId))).ToArray();
        return await coordinator.CreateAsync(requests, access.ActorId, request.RequestKey, request.ExpectedTotal);
    }
}
