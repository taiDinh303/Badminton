using Badminton.Core.Base;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Security;
using Badminton.Core.Store;
using Badminton.Core.Utils;
using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Points;
using Contract.Services.Payment;
using Contract.Services.Operations;
using Contract.Services.Booking;
using Contract.Services.Maintenance;
using Contract.Services.Voucher;
using Microsoft.Extensions.Options;
using ModelViews.Payment;

namespace Services;

public sealed class OnlineRefundService(IUnitOfWork uow, ICurrentUser currentUser, IOnlineRefundClient client,
    IOptions<OnlineRefundSettings> settings, IPointService points, IVoucherService vouchers, IAuditService audit,
    IBookingTransitionGuard transitions, IBookingCancellationAuthorization cancellation)
    : BaseService(uow, currentUser)
{
    public async Task<PaymentView> RequestAsync(int paymentId, int actorId, string? reason,
        bool cancel = false, bool maintenance = false, bool lateReceipt = false, MaintenanceBookingScope? maintenanceScope = null)
    {
        if (!settings.Value.Enabled) throw new ConflictException("ONLINE_REFUND_DISABLED: Chưa bật hoàn tiền online sau nghiệm thu sandbox.");
        OnlineRefundOperation operation = null!;
        Payment receipt = null!;
        var created = false;
        await Uow.RunInTransactionAsync(async () =>
        {
            await Uow.LockBookingRequestAsync($"online-refund:{paymentId}");
            receipt = await Repository<Payment>().GetByIdAsync(paymentId) ?? throw new NotFoundException("Payment", paymentId);
            await transitions.AcquireAsync(receipt.BookingId);
            var booking = (await Repository<Booking>().GetByConditionAsync(b => b.Id == receipt.BookingId)).SingleOrDefault()
                ?? throw new NotFoundException("Booking", receipt.BookingId);
            if (maintenance && maintenanceScope is not null) await transitions.EnsureMaintenanceScopeAsync(booking, maintenanceScope);
            if (cancel && !lateReceipt && !maintenance) await cancellation.EnsureAuthorizedAsync(booking, actorId);
            else await AuthorizeStaffBranchAsync(booking.BranchId, actorId);
            var existing = (await Repository<OnlineRefundOperation>().GetByConditionAsync(o => o.PaymentId == paymentId)).SingleOrDefault();
            if (existing is not null) { operation = existing; return; }
            if (!lateReceipt) await transitions.EnsureNoPendingRefundAsync(booking.Id);
            if (receipt.Status != PaymentStatus.Success || receipt.Method is PaymentMethod.Cash or PaymentMethod.QR)
                throw new ConflictException("Giao dịch không phải khoản thu online thành công.");
            if (lateReceipt)
            {
                if (!receipt.ReconciliationRequired || receipt.ReconciliationReason is not ("late_receipt" or "additional_receipt" or "confirmation_conflict"))
                    throw new ConflictException("Khoản thu không thuộc trường hợp hoàn tiền đối soát.");
            }
            else if (booking.DeletedAt.HasValue || booking.Status is not (BookingStatus.Confirmed or BookingStatus.Completed) || receipt.Amount != booking.PayableAmount)
                throw new ConflictException("Đơn hoặc số tiền thu không hợp lệ để hoàn.");
            var slot = await Repository<TimeSlot>().GetByIdAsync(booking.SlotId) ?? throw new NotFoundException("TimeSlot", booking.SlotId);
            var ratio = maintenance || lateReceipt ? 1.0 : RefundPolicy.Tier(HanoiTime.HanoiNow(), HanoiTime.SlotStart(booking.Date, slot.StartTime)).ratio;
            var amount = (int)(receipt.Amount * ratio);
            if (amount > 0 && (string.IsNullOrWhiteSpace(receipt.ProviderTransactionId) || string.IsNullOrWhiteSpace(receipt.GatewayRef)))
                throw new ConflictException("Thiếu mã giao dịch đã xác minh từ provider. Cần đối soát, không suy diễn mã giao dịch lịch sử.");
            operation = new OnlineRefundOperation { PaymentId = receipt.Id, BookingId = booking.Id,
                RequestId = client.CreateRequestId(receipt.Method), Amount = amount,
                Reason = string.IsNullOrWhiteSpace(reason) ? "Hoàn tiền đặt sân" : reason.Trim()[..Math.Min(reason.Trim().Length, 255)],
                ActorId = actorId, CancelBooking = cancel, ReleaseVoucher = cancel || maintenance, IsLateReceipt = lateReceipt };
            await Repository<OnlineRefundOperation>().AddAsync(operation);
            await Uow.Payments.RequireReviewAsync(receipt.Id, "refund_uncertain");
            await SaveAsync();
            created = true;
        }); // Durable intent commits BEFORE any provider IO.

        if (operation.Succeeded) return await ReceiptAsync(operation);
        var request = new OnlineRefundRequest(receipt.Method, operation.RequestId, receipt.GatewayRef ?? "",
            receipt.ProviderTransactionId ?? "", receipt.ProviderTransactionDate ?? "", operation.Amount, receipt.Amount, operation.Reason, operation.CreatedAt);
        OnlineRefundOutcome outcome;
        try { outcome = operation.Amount == 0 ? new(true, "no_refund_due") : await client.ExecuteAsync(request, queryOnly: !created); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or System.Text.Json.JsonException)
        { outcome = OnlineRefundOutcome.Pending(); }
        if (!outcome.Succeeded)
            throw new ConflictException("Đã lưu yêu cầu hoàn tiền nhưng kết quả chưa được xác nhận. Nhân viên truy vấn trong Đối soát; không tạo lệnh hoàn khác.");

        PaymentView result = null!;
        await Uow.RunInTransactionAsync(async () =>
        {
            await Uow.LockBookingRequestAsync($"online-refund:{paymentId}");
            var persisted = (await Repository<OnlineRefundOperation>().GetByConditionAsync(o => o.Id == operation.Id)).Single();
            if (persisted.Succeeded) { result = await ReceiptAsync(persisted); return; }
            await transitions.AcquireAsync(persisted.BookingId);
            var booking = (await Repository<Booking>().GetByConditionAsync(b => b.Id == persisted.BookingId)).Single();
            // Receipt refunds never resurrect or overwrite a terminal booking.
            if (!persisted.IsLateReceipt)
            {
                var changed = await Uow.Bookings.ClaimRefundAsync(booking.Id, persisted.CancelBooking, persisted.Reason, DateTime.UtcNow, booking.BranchId);
                if (changed == 0) throw new ConflictException("Provider đã hoàn tiền nhưng đơn vừa thay đổi; giữ yêu cầu đối soát để kiểm tra.");
                if (persisted.Amount > 0 && await points.HasEarnedRowAsync(booking.Id))
                    await points.ClawbackAsync(booking.UserId, booking.Id, persisted.Amount, "online refund " + booking.BookingCode);
                if (persisted.Amount == receipt.Amount && booking.PointsRedeemed > 0)
                    await points.RestoreAsync(booking.UserId, booking.Id, booking.PointsRedeemed, booking.BookingCode);
                if (persisted.ReleaseVoucher && booking.VoucherRedemptionId is { } rid) await vouchers.ReleaseVoucherAsync(rid);
            }
            var row = new Payment { BookingId = booking.Id, UserId = receipt.UserId, Method = receipt.Method,
                Amount = receipt.Amount, RefundAmount = persisted.Amount, RefundedAt = DateTime.UtcNow,
                Status = persisted.Amount > 0 && persisted.Amount < receipt.Amount ? PaymentStatus.PartialRefund : PaymentStatus.Refunded,
                GatewayRef = "ONLINE-REFUND-" + persisted.RequestId, RawResponse = persisted.Reason };
            await Repository<Payment>().AddAsync(row);
            await Uow.Payments.CompleteOnlineRefundAsync(persisted.Id);
            await Uow.Payments.ClearRefundReviewAsync(receipt.Id);
            await SaveAsync();
            await audit.LogAsync(persisted.ActorId, "payment.online-refund", "Payment", row.Id.ToString(), null,
                new { row.RefundAmount, persisted.RequestId, persisted.PaymentId }, persisted.Reason);
            result = View(row);
        });
        return result;
    }

    private async Task<PaymentView> ReceiptAsync(OnlineRefundOperation operation) => View(
        (await Repository<Payment>().GetByConditionAsync(p => p.GatewayRef == "ONLINE-REFUND-" + operation.RequestId)).Single());
    private static PaymentView View(Payment p) => new(p.Id, p.BookingId, p.Method.ToString(), p.Amount, p.Status.ToString(), p.GatewayRef, p.PaidAt, p.RefundAmount, p.RefundedAt);
}
