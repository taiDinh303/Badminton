using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Badminton.Core.Base;
using ModelViews.Booking;

namespace Services;

public static class BookingRequestIdentity
{
    public static string Fingerprint(CreateBookingRequest request, int userId)
    {
        if (!DateOnly.TryParse(request.Date, out var date)) throw new BadRequestException("Ngày không hợp lệ.");
        var payload = JsonSerializer.Serialize(new
        {
            Version = 1, UserId = userId, request.CourtId, Date = date.ToString("yyyy-MM-dd"),
            request.SlotId, request.PlayerCount, request.GroupId,
            VoucherCode = string.IsNullOrWhiteSpace(request.VoucherCode) ? null : request.VoucherCode.Trim(),
            PointsToRedeem = request.PointsToRedeem ?? 0, Note = request.Note ?? ""
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
