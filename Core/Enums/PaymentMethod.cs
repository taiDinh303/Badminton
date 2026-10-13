namespace Core.Enums
{
    /// <summary>
    /// Phương thức thanh toán cho bản đặt sân.
    /// </summary>
    public enum PaymentMethod
    {
        /// <summary>Thanh toán bằng tiền mặt tại sân.</summary>
        Cash = 1,

        /// <summary>Chuyển khoản ngân hàng.</summary>
        BankTransfer = 2,

        /// <summary>Thanh toán qua thẻ (Visa/MasterCard).</summary>
        Card = 3,

        /// <summary>Mã QR (Momo, VietQR).</summary>
        QrCode = 4
    }
}
