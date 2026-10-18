namespace Contract.Repositories.Entity
{
    public class VoucherRedemption
    {
        public int Id { get; set; }
        public int VoucherId { get; set; }
        public Voucher? Voucher { get; set; }
        public int UserId { get; set; }
        public User? User { get; set; }
        public int? BookingId { get; set; }
        public Booking? Booking { get; set; }
        public int DiscountAmount { get; set; }
        public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;
    }
}
