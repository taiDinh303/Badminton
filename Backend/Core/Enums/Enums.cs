namespace Badminton.Core.Enums
{
    // ── Roles (customer/manager/admin) ──
    public enum Role
    {
        Customer, Manager, Admin
    }

    // ── Branch (active/closed) ──
    public enum BranchStatus
    {
        Active, Closed
    }

    // ── Court date status (open/maintenance/closed) ──
    public enum CourtDateStatus
    {
        Open, Maintenance, Closed
    }

    // ── Court surface (sindalo/carpet/wood) ──
    public enum CourtSurface
    {
        Sindalo, Carpet, Wood
    }

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
    public enum PaymentMethod
    {
        VnPay, MoMo, Zalopay, Cash, QR
    }

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
    public enum VoucherType
    {
        Percent, Fixed
    }

    // ── User status (active/locked) ──
    public enum UserStatus
    {
        Active, Locked
    }

    // ── Membership tier (V2: points multiplier + benefits) ──

    // None = default customer (1x points). Silver = 1.5x. Gold = 2x.
    public enum MembershipTier
    {
        None, Silver, Gold
    }

    // ── Notification business events (outbox rows) ──
    public enum NotifyEvent
    {
        BookingConfirmed,
        BookingExpired,
        NoShowRecorded,
        WaitlistPromoted,
        BookingReminder,
        BranchIncident
    }

    // ── Notification delivery status (per outbox row, per channel) ──
    public enum NotifyStatus
    {
        Pending,
        Delivered,
        Failed,
        Skipped
    }

    // ── Waitlist entry status ──
    public enum WaitlistStatus
    {
        Waiting,
        Promoted,
        Declined,
        Cancelled,
        Expired
    }
}
