namespace M.Core.Enums
{
    // ── Roles (customer/manager/admin) ──
    public enum Role { Customer, Manager, Admin }

    // ── Branch (active/closed) ──
    public enum BranchStatus { Active, Closed }

    // ── Court date status (open/maintenance/closed) ──
    public enum CourtDateStatus { Open, Maintenance, Closed }

    // ── Court surface (sindalo/carpet/wood) ──
    public enum CourtSurface { Sindalo, Carpet, Wood }

    // ── Booking (pending/confirmed/completed/cancelled/no_show/refunded) ──
    public enum BookingStatus
    {
        Pending,
        Confirmed,
        Completed,
        Cancelled,
        NoShow,
        Refunded
    }

    // ── Payment (vnpay/momo/zalopay/cash/qr) ──
    public enum PaymentMethod { VnPay, MoMo, Zalopay, Cash, QR }

    // ── Payment status (pending/success/failed/refunded/partial_refund) ──
    public enum PaymentStatus
    {
        Pending,
        Success,
        Failed,
        Refunded,
        PartialRefund
    }

    // ── Voucher (percent/fixed) ──
    public enum VoucherType { Percent, Fixed }

    // ── User status (active/locked) ──
    public enum UserStatus { Active, Locked }
}
