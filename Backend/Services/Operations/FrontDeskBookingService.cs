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

public sealed class FrontDeskBookingService(IUnitOfWork uow, IOperationsAccess access, IBookingBatchCoordinator coordinator) : IFrontDeskBookingService
{
    public async Task<DeskCustomerView?> FindCustomerAsync(string phone)
    {
        await access.StaffBranchAsync();
        phone = AuthInput.Phone(phone);
        var user = (await uow.GetRepository<User>().GetByConditionAsync(u => u.PhoneNumber == phone && u.Role == Badminton.Core.Enums.Role.Customer && u.DeletedAt == null && u.Status == Badminton.Core.Enums.UserStatus.Active)).SingleOrDefault();
        return user is null ? null : new(user.Id, user.FullName, user.IsVerified);
    }
    public async Task<BookingView> CreateForCustomerAsync(DeskBookingRequest request)
    {
        var court = await uow.GetRepository<Court>().GetByIdAsync(request.CourtId) ?? throw new NotFoundException("Court", request.CourtId);
        await access.StaffBranchAsync(court.BranchId);
        var rows = await coordinator.CreateAsync([new(request.CourtId, request.Date.ToString("yyyy-MM-dd"), request.SlotId, request.PlayerCount)], request.CustomerId, request.RequestKey);
        return rows[0];
    }
}
