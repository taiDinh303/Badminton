using ModelViews.BookingModelView;

namespace Contract.Services.Interface.Booking
{
    /// <summary>
    /// Nghiệp vụ đặt sân – thực thi đa hình qua chiến lược giá + bộ xử lý thanh toán.
    /// </summary>
    public interface IBookingService
    {
        Task<List<BookingResponseModelView>> GetAllAsync();

        Task<BookingResponseModelView> GetByIdAsync(Guid id);

        Task<List<BookingResponseModelView>> GetByUserIdAsync(string userInfoId);

        /// <summary>
        /// Tạo bản đặt: kiểm tra khung giờ trống, tính giá theo chiến lược, chiếm khung giờ.
        /// </summary>
        Task<BookingResponseModelView> CreateAsync(CreateBookingModelView model);

        /// <summary>Cập nhật trạng thái / phương thức thanh toán.</summary>
        Task<BookingResponseModelView> UpdateAsync(UpdateBookingModelView model);

        /// <summary>Hủy booking (trước deadline hoàn tiền).</summary>
        Task CancelAsync(Guid id);

        Task SoftDeleteAsync(Guid id);

        Task DeleteAsync(Guid id);
    }
}
