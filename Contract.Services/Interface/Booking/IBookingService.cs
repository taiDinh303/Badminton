using ModelViews.BookingModelView;

namespace Contract.Services.Interface.Booking
{
    public interface IBookingService
    {
        Task<List<BookingResponseModelView>> GetAllAsync();

        Task<BookingResponseModelView> GetByIdAsync(Guid id);

        Task<List<BookingResponseModelView>> GetByUserIdAsync(
            string userInfoId);

        Task CreateAsync(
            CreateBookingModelView model);

        Task UpdateAsync(
            UpdateBookingModelView model);

        Task DeleteAsync(Guid id);
    }
}