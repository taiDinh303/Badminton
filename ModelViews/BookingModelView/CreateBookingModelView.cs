using System.ComponentModel.DataAnnotations;

namespace ModelViews.BookingModelView
{
    public class CreateBookingModelView
    {
        [Required]
        public DateTime BookingDate { get; set; }

        [Required]
        public string UserInfoId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string BankAccountID { get; set; } = string.Empty;

        [Required]
        public string CalendarTypeID { get; set; } = string.Empty;

        public decimal Price { get; set; }
    }
}