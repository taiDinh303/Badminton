namespace ModelViews.BookingModelView
{
    public class UpdateBookingModelView
    {
        public Guid Id { get; set; }

        public DateTime BookingDate { get; set; }

        public decimal Price { get; set; }

        public bool PaymentStatus { get; set; }

        public string UserInfoId { get; set; } = string.Empty;

        public string BankAccountID { get; set; } = string.Empty;

        public string CalendarTypeID { get; set; } = string.Empty;
    }
}