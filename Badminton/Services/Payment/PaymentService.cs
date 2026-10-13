using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Payment;
using Contract.Services.Points;
using M.Core.Base;
using M.Core.Domain;
using M.Core.Enums;
using Microsoft.AspNetCore.Http;
using ModelViews.Payment;

namespace Services
{
    public sealed class PaymentService : BaseService, IPaymentService
    {
        private readonly IPaymentGatewayFactory _gateways;
        private readonly IPointService _points;
        private readonly IAuditService _audit;

        public PaymentService(IUnitOfWork uow, IHttpContextAccessor http,
            IPaymentGatewayFactory gateways, IPointService points, IAuditService audit)
            : base(uow, http)
        {
            _gateways = gateways;
            _points = points;
            _audit = audit;
        }

        private IGenericRepository<Payment> Repo => Repository<Payment>();
        private IGenericRepository<Booking> Bookings => Repository<Booking>();

        public async Task<PaymentInitResponse> InitAsync(InitPaymentRequest request, int userId)
        {
            var booking = await Bookings.GetByIdAsync(request.BookingId);
            if (booking is null) throw new NotFoundException("Booking", request.BookingId);
            if (booking.UserId != userId && !IsStaff())
                throw new ConflictException("Khong co quyen thao tac don nay.");
            if (booking.Status is not BookingStatus.Pending)
                throw new ConflictException($"Don {booking.BookingCode} da {booking.Status}, khong can khoi tao thanh toan.");

            var method = ParseMethod(request.Method);
            var gateway = _gateways.GetGateway(method);

            // On-site methods have no gateway URL - settled by a manager.
            if (method is PaymentMethod.Cash or PaymentMethod.QR)
                return new PaymentInitResponse(method.ToString(), null, null, booking.BookingCode);

            var init = await gateway.InitAsync(booking.BookingCode, booking.PayableAmount,
                request.ReturnUrl ?? "/api/v1/bookings/" + booking.Id, default);
            if (!init.Ok)
                throw new BadRequestException(init.Error ?? "Khong the khoi tao thanh toan.");

            // Pending payment row carries the gateway_ref (idempotency key).
            var payment = new Payment
            {
                BookingId = booking.Id,
                UserId = userId,
                Amount = booking.PayableAmount,
                Method = method,
                GatewayRef = init.GatewayRef,
                Status = PaymentStatus.Pending
            };
            _ = Repo.AddAsync(payment);
            await SaveAsync();

            return new PaymentInitResponse(method.ToString(), init.GatewayUrl,
                init.GatewayRef, booking.BookingCode);
        }

        public async Task HandleWebhookAsync(string provider, PaymentWebhookRequest raw)
        {
            if (string.IsNullOrWhiteSpace(raw.GatewayRef))
                throw new BadRequestException("webhook thieu gateway_ref.");

            // IDEMPOTENCY: look up by gateway_ref, not by booking.
            var rows = await Repo.GetByConditionAsync(p => p.GatewayRef == raw.GatewayRef);
            var payment = rows.FirstOrDefault();
            if (payment is null)
                throw new NotFoundException("Payment", raw.GatewayRef);

            if (payment.Status == PaymentStatus.Success)
                return;

            var booking = await Bookings.GetByIdAsync(payment.BookingId);
            if (booking is null || booking.Status is BookingStatus.Cancelled
                or BookingStatus.NoShow or BookingStatus.Refunded)
                return;

            // webhook = source of truth: mark success + confirmed + award points.
            payment.Status = PaymentStatus.Success;
            payment.PaidAt = DateTime.UtcNow;
            payment.RawResponse = raw.RawResponse;
            Repo.Update(payment);

            booking.Status = BookingStatus.Confirmed;
            booking.PointsEarned = booking.PayableAmount; // 1 dong = 1 point
            booking.UpdatedAt = DateTime.UtcNow;
            Bookings.Update(booking);

            await _points.AwardAsync(booking.UserId, booking.Id, booking.PointsEarned,
                "confirmed booking " + booking.BookingCode);
            await SaveAsync();
        }

        public async Task<PaymentView> ConfirmOnSiteAsync(int bookingId, string method,
            int actorId, string? note = null)
        {
            var booking = await Bookings.GetByIdAsync(bookingId);
            if (booking is null) throw new NotFoundException("Booking", bookingId);
            if (booking.Status is not BookingStatus.Pending)
                throw new ConflictException($"Don {booking.BookingCode} da {booking.Status}.");

            var m = ParseMethod(method);
            if (m is not (PaymentMethod.Cash or PaymentMethod.QR))
                throw new BadRequestException("Thanh toan tai quay chi dung Cash/QR.");

            var payment = new Payment
            {
                BookingId = booking.Id,
                UserId = booking.UserId,
                Amount = booking.PayableAmount,
                Method = m,
                Status = PaymentStatus.Success,
                PaidAt = DateTime.UtcNow,
                RawResponse = note
            };
            _ = Repo.AddAsync(payment);

            booking.Status = BookingStatus.Confirmed;
            booking.PointsEarned = booking.PayableAmount;
            booking.UpdatedAt = DateTime.UtcNow;
            Bookings.Update(booking);

            await _points.AwardAsync(booking.UserId, booking.Id, booking.PointsEarned,
                "on-site confirmed " + booking.BookingCode);
            await SaveAsync();
            await _audit.LogAsync(actorId, "payment.on-site-confirm", "Payment",
                payment.Id.ToString(), null,
                new { payment.Method, payment.Amount, payment.Status, payment.GatewayRef, payment.PaidAt },
                note);
            return ToView(payment);
        }

        public async Task<PaymentView> RefundAsync(int bookingId, int actorId, string? reason = null)
        {
            var booking = await Bookings.GetByIdAsync(bookingId);
            if (booking is null) throw new NotFoundException("Booking", bookingId);

            // An unpaid (pending) booking: just release the slot.
            if (booking.Status is BookingStatus.Pending)
                throw new ConflictException(
                    "Don chua thanh toan: huy don de giai phong slot, khong phat sinh hoan tien.");

            if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Completed))
                throw new ConflictException($"Khong the hoan tien don {booking.Status}.");

            var slot = await Repository<TimeSlot>().GetByIdAsync(booking.SlotId);
            if (slot is null)
                throw new ConflictException("Khung gio cua don bi mat, khong the tinh ty le hoan tien.");
            var slotStartUtc = booking.Date.ToDateTime(slot.StartTime);
            var (ratio, label) = RefundPolicy.Tier(DateTime.UtcNow, slotStartUtc);

            var payments = await Repo.GetByConditionAsync(p => p.BookingId == booking.Id);
            var refundRow = new Payment
            {
                BookingId = booking.Id,
                UserId = booking.UserId,
                Method = payments.FirstOrDefault()?.Method ?? PaymentMethod.Cash,
                Amount = booking.PayableAmount,
                Status = PaymentStatus.Refunded,
                RefundAmount = (int)(booking.PayableAmount * ratio),
                RefundedAt = DateTime.UtcNow,
                GatewayRef = $"REFUND-{booking.Id}-{Guid.NewGuid():N}"[..20],
                RawResponse = label
            };
            _ = Repo.AddAsync(refundRow);

            booking.Status = BookingStatus.Refunded;
            booking.CancelReason = label;
            booking.UpdatedAt = DateTime.UtcNow;
            Bookings.Update(booking);

            if (booking.PointsEarned > 0 && ratio == 0)
                await _points.ClawbackAsync(booking.UserId, booking.Id, booking.PointsEarned,
                    "full refund of " + booking.BookingCode);

            await SaveAsync();
            await _audit.LogAsync(actorId, "payment.refund", "Payment", refundRow.Id.ToString(),
                null, refundRow, reason ?? label);
            return ToView(refundRow);
        }

        /// <summary>Read payments for a booking.</summary>
        public async Task<IReadOnlyList<PaymentView>> GetByBookingAsync(int bookingId)
        {
            var list = await Repo.GetByConditionAsync(p => p.BookingId == bookingId);
            return list.Select(ToView).ToList();
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private bool IsStaff() =>
            _http.HttpContext?.User.IsInRole("Admin") == true
            || _http.HttpContext?.User.IsInRole("Manager") == true;

        private static PaymentMethod ParseMethod(string s) =>
            Enum.TryParse<PaymentMethod>(s, ignoreCase: true, out var m)
                ? m
                : throw new BadRequestException($"Phuong thuc thanh toan '{s}' khong hop le.");

        private static PaymentView ToView(Payment p) => new(
            p.Id, p.BookingId, p.Method.ToString(), p.Amount, p.Status.ToString(),
            p.GatewayRef, p.PaidAt, p.RefundAmount, p.RefundedAt);
    }
}
