namespace M.Core.Enums
{
    /// <summary>Status of a court. Maps the SQL CHECK constraint on dbo.Courts.Status.</summary>
    public enum CourtStatus
    {
        Available,
        Maintenance,
        Inactive
    }

    /// <summary>Status of a booking. Maps the SQL CHECK constraint on dbo.Bookings.Status.</summary>
    public enum BookingStatus
    {
        Pending,
        Confirmed,
        Paid,
        Cancelled,
        Completed,
        Expired
    }

    /// <summary>Status of a payment. Maps the SQL CHECK constraint on dbo.Payments.Status.</summary>
    public enum PaymentStatus
    {
        Pending,
        Paid,
        Failed,
        Refunded
    }

    /// <summary>
    /// Payment methods. This is a POLYMORPHISM driver (đa hình):
    /// each value maps to a concrete <c>IPaymentStrategy</c> implementation,
    /// so adding a new payment method is a new class, not a new if/else.
    /// </summary>
    public enum PaymentMethod
    {
        Cash,
        BankTransfer,
        QRCode
    }

    /// <summary>Status of a booking detail (slot) row.</summary>
    public enum BookingDetailStatus
    {
        Reserved,
        Cancelled,
        Completed
    }

    /// <summary>Status of a customer check-in/out record.</summary>
    public enum CheckInStatus
    {
        Pending,
        CheckedIn,
        CheckedOut,
        NoShow
    }
}
