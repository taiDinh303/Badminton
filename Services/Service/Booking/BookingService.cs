using Contract.Repositories.Entity;
using Contract.Repositories.IUnitOfWork;
using Contract.Services.Interface.Booking;
using Core.Base;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelViews.BookingModelView;
using Services.Mappings;
using System.IdentityModel.Tokens.Jwt;
using static Core.Base.BaseException;
using BookingEntity = Contract.Repositories.Entity.Booking;

namespace Services.Service.Booking
{
    public class BookingService : IBookingService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BookingService(
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<List<BookingResponseModelView>> GetAllAsync()
        {
            var bookings = await _unitOfWork
                .GetRepository<BookingEntity>()
                .Entities
                .Where(x => !x.DeletedTime.HasValue)
                .OrderByDescending(x => x.CreatedTime)
                .ToListAsync();

            return bookings.Select(x => x.ToViewModel()).ToList();
        }

        public async Task<BookingResponseModelView> GetByIdAsync(Guid id)
        {
            var booking = await FindBookingAsync(id);

            if (!IsAdmin() && !IsOwner(booking))
                throw Forbidden();

            return booking.ToViewModel();
        }

        public async Task<List<BookingResponseModelView>> GetByUserIdAsync(string userInfoId)
        {
            if (!IsAdmin())
            {
                string? currentUserId = GetCurrentUserId();
                if (string.IsNullOrEmpty(currentUserId) || currentUserId != userInfoId)
                    throw Forbidden();
            }

            var bookings = await _unitOfWork
                .GetRepository<BookingEntity>()
                .Entities
                .Where(x =>
                    x.UserInfoId == userInfoId &&
                    !x.DeletedTime.HasValue)
                .OrderByDescending(x => x.BookingDate)
                .ToListAsync();

            return bookings.Select(x => x.ToViewModel()).ToList();
        }

        public async Task CreateAsync(CreateBookingModelView model)
        {
            string userId = GetCurrentUserId()
                ?? throw new ErrorException(
                    StatusCodes.Status401Unauthorized,
                    ResponseCodeConstants.UNAUTHORIZED,
                    "You are not authenticated.");

            var userInfo = await _unitOfWork
                .GetRepository<UserInfo>()
                .Entities
                .FirstOrDefaultAsync(x =>
                    x.Id.ToString() == userId &&
                    !x.DeletedTime.HasValue);

            if (userInfo == null)
                throw NotFound("User not found");

            var booking = new BookingEntity
            {
                BookingDate = model.BookingDate,
                BookingDeadline = model.BookingDate.Date.AddDays(1).AddTicks(-1),
                Price = model.Price,
                PaymentStatus = false,
                UserInfoId = userId,
                UserName = $"{userInfo.GivenName} {userInfo.FamilyName}".Trim(),
                PhoneNumber = string.Empty,
                BankAccountID = model.BankAccountID,
                CalendarTypeID = model.CalendarTypeID
            };

            await _unitOfWork
                .GetRepository<BookingEntity>()
                .InsertAsync(booking);

            await _unitOfWork.SaveAsync();
        }

        public async Task UpdateAsync(UpdateBookingModelView model)
        {
            var repo = _unitOfWork.GetRepository<BookingEntity>();
            var booking = await FindBookingAsync(model.Id);

            if (!IsAdmin() && !IsOwner(booking))
                throw Forbidden();

            booking.BookingDate = model.BookingDate;
            booking.Price = model.Price;
            booking.PaymentStatus = model.PaymentStatus;
            booking.BankAccountID = model.BankAccountID;
            booking.CalendarTypeID = model.CalendarTypeID;
            booking.BookingDeadline = model.BookingDate.Date.AddDays(1).AddTicks(-1);
            booking.LastUpdatedBy = GetCurrentUserId();
            booking.LastUpdatedTime = Core.Utils.CoreHelper.SystemTimeNow;

            await repo.UpdateAsync(booking);
            await _unitOfWork.SaveAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var repo = _unitOfWork.GetRepository<BookingEntity>();
            var booking = await FindBookingAsync(id);

            if (!IsAdmin() && !IsOwner(booking))
                throw Forbidden();

            booking.DeletedBy = GetCurrentUserId();
            booking.DeletedTime = Core.Utils.CoreHelper.SystemTimeNow;

            await repo.UpdateAsync(booking);
            await _unitOfWork.SaveAsync();
        }

        private async Task<BookingEntity> FindBookingAsync(Guid id)
        {
            return await _unitOfWork
                .GetRepository<BookingEntity>()
                .Entities
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    !x.DeletedTime.HasValue)
                ?? throw NotFound("Booking not found");
        }

        private string? GetCurrentUserId()
        {
            return _httpContextAccessor.HttpContext?
                .User
                .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        }

        private bool IsAdmin()
        {
            return _httpContextAccessor.HttpContext?
                .User
                .IsInRole("Admin") == true;
        }

        private bool IsOwner(BookingEntity booking)
        {
            string? currentUserId = GetCurrentUserId();
            return !string.IsNullOrEmpty(currentUserId) &&
                   booking.UserInfoId == currentUserId;
        }

        private static ErrorException NotFound(string message)
        {
            return new ErrorException(
                StatusCodes.Status404NotFound,
                ResponseCodeConstants.NOT_FOUND,
                message);
        }

        private static ErrorException Forbidden()
        {
            return new ErrorException(
                StatusCodes.Status403Forbidden,
                ResponseCodeConstants.FORBIDDEN,
                "You do not have permission to access this booking.");
        }
    }
}
