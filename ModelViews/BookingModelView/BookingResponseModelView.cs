namespace ModelViews.BookingModelView
{
    public class BookingResponseModelView
    {
        public Guid Id { get; set; }

        public DateTime BookingDate { get; set; }

        public DateTime? BookingDeadline { get; set; }

        public decimal Price { get; set; }

        public bool PaymentStatus { get; set; }

        public string UserInfoId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public string BankAccountID { get; set; } = string.Empty;

        public string CalendarTypeID { get; set; } = string.Empty;

        public DateTimeOffset CreatedTime { get; set; }
    }
}