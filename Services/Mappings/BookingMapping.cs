using Contract.Repositories.Entity;
using ModelViews.BookingModelView;

namespace Services.Mappings
{
    public static class BookingMapping
    {
        public static BookingResponseModelView ToViewModel(
            this Booking entity)
        {
            return new BookingResponseModelView
            {
                Id = entity.Id,
                BookingDate = entity.BookingDate,
                BookingDeadline = entity.BookingDeadline,
                Price = entity.Price,
                PaymentStatus = entity.PaymentStatus,
                UserInfoId = entity.UserInfoId,
                UserName = entity.UserName,
                PhoneNumber = entity.PhoneNumber,
                BankAccountID = entity.BankAccountID,
                CalendarTypeID = entity.CalendarTypeID,
                CreatedTime = entity.CreatedTime
            };
        }

        public static Booking ToEntity(
            this CreateBookingModelView model)
        {
            return new Booking
            {
                BookingDate = model.BookingDate,
                UserInfoId = model.UserInfoId,
                UserName = model.UserName,
                PhoneNumber = model.PhoneNumber,
                BankAccountID = model.BankAccountID,
                CalendarTypeID = model.CalendarTypeID,
                Price = model.Price
            };
        }
    }
}