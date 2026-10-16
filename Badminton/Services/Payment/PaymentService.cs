using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Payment;
using Contract.Services.Points;
using M.Core.Base;
using M.Core.Domain;
using M.Core.Enums;
using M.Core.Utils;
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

        private static readonly IReadOnlyDictionary<string, string> EmptyHeaders
            = new Dictionary<string, string>();

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

        // ── Webhook: verified → idempotent → atomic ────────────────────────
        public async Task<WebhookResult> HandleWebhookAsync(string provider, PaymentWebhookRequest raw)
        {
            if (string.IsNullOrWhiteSpace(raw.GatewayRef))
                return WebhookResult.Reject("webhook thieu gateway_ref.");

            // SECURITY: never trust a webhook before the provider's signature
            // is verified. Resolve THIS provider's gateway and verify the raw.
            IPaymentGateway gateway;
            try { gateway = _gateways.GetGatewayByProvider(provider); }
            catch (BadRequestException ex) { return WebhookResult.Reject(ex.Message); }

            // Feed the gateway what it needs to verify. When the caller only
            // reported a status (no raw body), synthesise a minimal body so the
            // provider adapter derives success/failure from the reported status
            // instead of defaulting to success on a blank payload.
            var verifyBody = string.IsNullOrEmpty(raw.RawResponse)
                ? "{\"status\":\"" + (raw.Status ?? string.Empty).ToLowerInvariant() + "\"}"
                : raw.RawResponse;
            var verified = await gateway.VerifyWebhookAsync(
                new RawWebhook(provider, raw.GatewayRef, verifyBody,
                    raw.Status, EmptyHeaders));
            if (!verified.Verified)
                return WebhookResult.Reject(verified.Error ?? "webhook chua duoc xac minh ky hieu.");

            // IDEMPOTENCY + EXISTENCE: look up the payment row for this ref.
            //   - no row at all      → unknown ref → REJECT (not a replay).
            //   - row already Success → a clean replay → DUPLICATE no-op.
            //   - row Pending/Failed  → proceed to claim it below.
            var known = await Repo.GetByConditionAsync(
                p => p.GatewayRef == raw.GatewayRef);
            if (known.Count == 0)
                return WebhookResult.Reject("gateway_ref khong ton tai trong he thong.");
            if (known.First().Status == PaymentStatus.Success)
                return WebhookResult.Duplicate("gateway_ref da duoc xu ly truoc do.");

            // A failure never flips the booking: mark the payment failed and
            // let the customer retry / cancel.
            if (!verified.Success)
            {
                await Uow.ExecuteSqlRawAsync(
                    "UPDATE Payments SET Status = 'Failed' " +
                    "WHERE GatewayRef = @p0 AND Status = 'Pending'",
                    raw.GatewayRef);
                await SaveAsync();
                return WebhookResult.Reject("gateway bao loi, don vao tung pending/failed.");
            }

            // ATOMICITY: flip payment Pending→Success and booking
            // Pending→Confirmed (+points) inside one database transaction,
            // with guarded CAS so only a single concurrent writer wins.
            var refNo = raw.GatewayRef;
            int confirmed = 0;
            int bookingId = 0;
            int points = 0;
            string bookingCode = string.Empty;
            int userId = 0;

            await Uow.RunInTransactionAsync(async () =>
            {
                // 1) claim the payment (single writer wins; loser gets 0).
                // Empty body → store an empty string (RawResponse is nullable
                // but "" is a valid, non-null value; ExecuteSqlRaw has no
                // DBNull mapping).
                string rawBody = Trunc(raw.RawResponse) ?? string.Empty;
                // Allow Failed→Success too: a gateway may report a failure
                // webhook first and then re-notify success for the same ref;
                // the booking must still be confirmable instead of rotting
                // until the pending-expiry job cancels it.
                int claimed = await Uow.ExecuteSqlRawAsync(
                    "UPDATE Payments SET Status = 'Success', PaidAt = @p0, " +
                    "  RawResponse = @p1 " +
                    "WHERE GatewayRef = @p2 AND Status IN ('Pending', 'Failed')",
                    DateTime.UtcNow, rawBody, refNo);
                if (claimed == 0) return; // lost a same-instant race → skip

                var p = (await Repo.GetByConditionAsync(x => x.GatewayRef == refNo)).First();
                var booking = await Bookings.GetByIdAsync(p.BookingId);
                if (booking is null ||
                    booking.Status is BookingStatus.Cancelled or BookingStatus.NoShow or
                    BookingStatus.Refunded)
                    return; // terminal: keep the payment record, do not confirm

                // 2) flip the booking exactly once.
                points = booking.PayableAmount;
                bookingId = booking.Id;
                bookingCode = booking.BookingCode;
                userId = booking.UserId;
                confirmed = await Uow.ExecuteSqlRawAsync(
                    "UPDATE Bookings SET Status = 'Confirmed', PointsEarned = @p0, " +
                    "  UpdatedAt = @p1 " +
                    "WHERE Id = @p2 AND Status = 'Pending'",
                    points, DateTime.UtcNow, bookingId);

                if (confirmed > 0)
                    await _points.AwardAsync(userId, bookingId, points,
                        "confirmed booking " + bookingCode);

                await SaveAsync();
            });

            return confirmed > 0
                ? WebhookResult.Applied("don da duoc xac nhan.")
                : WebhookResult.Duplicate("webhook dong bo (dupe/race), da co don xu ly truoc do.");
        }

        public async Task<PaymentView> ConfirmOnSiteAsync(int bookingId, string method,
            int actorId, string? note = null)
        {
            var booking = await Bookings.GetByIdAsync(bookingId);
            if (booking is null) throw new NotFoundException("Booking", bookingId);
            await AuthorizeStaffScope(booking, actorId);
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
                RawResponse = Trunc(note)
            };

            int confirmed = 0;
            await Uow.RunInTransactionAsync(async () =>
            {
                // One manager can confirm; a racing webhook / second manager
                // is rejected by the same Pending→Confirmed guard.
                confirmed = await Uow.ExecuteSqlRawAsync(
                    "UPDATE Bookings SET Status = 'Confirmed', PointsEarned = @p0, " +
                    "  UpdatedAt = @p1 " +
                    "WHERE Id = @p2 AND Status = 'Pending'",
                    booking.PayableAmount, DateTime.UtcNow, bookingId);
                if (confirmed == 0)
                    throw new ConflictException(
                        $"Don {booking.BookingCode} da duoc xac nhan truoc do.");

                _ = Repo.AddAsync(payment);
                await _points.AwardAsync(booking.UserId, booking.Id,
                    booking.PayableAmount, "on-site confirmed " + booking.BookingCode);
                await SaveAsync();
            });

            await _audit.LogAsync(actorId, "payment.on-site-confirm", "Payment",
                payment.Id.ToString(), null,
                new { payment.Method, payment.Amount, payment.Status, payment.GatewayRef, payment.PaidAt },
                note);
            return ToView(payment);
        }

        // ── Refund: claim → gateway → record (race-safe + idempotent) ──────

        private static System.Threading.SemaphoreSlim GetRefundLock(int bookingId) =>
            _refundLocks.GetOrAdd(bookingId, _ => new System.Threading.SemaphoreSlim(1, 1));

        private static void ReleaseRefundLock(int bookingId, System.Threading.SemaphoreSlim lockObj)
        {
            lockObj.Release();
            _refundLocks.TryRemove(bookingId, out _);
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int,
            System.Threading.SemaphoreSlim> _refundLocks = new();

        public async Task<PaymentView> RefundAsync(int bookingId, int actorId, string? reason = null)
        {
            var refundLock = GetRefundLock(bookingId);
            await refundLock.WaitAsync();
            try
            {
                var booking = await Bookings.GetByIdAsync(bookingId);
                if (booking is null) throw new NotFoundException("Booking", bookingId);
                await AuthorizeStaffScope(booking, actorId);

                // IDEMPOTENCY FIRST: a replayed / already-processed refund
                // returns the existing record, regardless of current status.
                // This runs before the status guard so a replay after the
                // booking is already Refunded still resolves cleanly.
                var existingRefund = (await Repo.GetByConditionAsync(
                    p => p.BookingId == bookingId &&
                         (p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartialRefund)))
                    .FirstOrDefault();
                if (existingRefund is not null)
                    return ToView(existingRefund);

                // An unpaid (pending) booking: just release the slot.
                if (booking.Status is BookingStatus.Pending)
                    throw new ConflictException(
                        "Don chua thanh toan: huy don de giai phong slot, khong phat sinh hoan tien.");

                if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Completed))
                    throw new ConflictException($"Khong the hoan tien don {booking.Status}.");

                var slot = await Repository<TimeSlot>().GetByIdAsync(booking.SlotId);
                if (slot is null)
                    throw new ConflictException("Khung gio cua don bi mat, khong the tinh ty le hoan tien.");
                // Refund tiers are computed in the business wall clock (Asia/Ho_Chi_Minh).
                // slotStart is naive Hanoi, so compare against HanoiTime.HanoiNow().
                var slotStart = HanoiTime.SlotStart(booking.Date, slot.StartTime);
                var (ratio, label) = RefundPolicy.Tier(HanoiTime.HanoiNow(), slotStart);
                var refundAmount = (int)(booking.PayableAmount * ratio);

                var original = (await Repo.GetByConditionAsync(
                    p => p.BookingId == bookingId && p.Status == PaymentStatus.Success))
                    .FirstOrDefault();
                var method = original?.Method ?? PaymentMethod.Cash;

                // CLAIM: atomically flip the booking to Refunded so exactly one
                // of two concurrent refund calls proceeds (the lock above plus
                // this guard = defence in depth).
                int claimed = await Uow.ExecuteSqlRawAsync(
                    "UPDATE Bookings SET Status = 'Refunded', CancelReason = @p0, " +
                    "  UpdatedAt = @p1 " +
                    "WHERE Id = @p2 AND Status IN ('Confirmed','Completed')",
                    label, DateTime.UtcNow, bookingId);
                if (claimed == 0)
                {
                    var winner = (await Repo.GetByConditionAsync(
                        p => p.BookingId == bookingId &&
                             (p.Status == PaymentStatus.Refunded ||
                              p.Status == PaymentStatus.PartialRefund)))
                        .FirstOrDefault();
                    return winner is not null
                        ? ToView(winner)
                        : throw new ConflictException("Don vung: hoan tien dong thoi bi don khac chiem.");
                }

                // External call (outside the DB transaction): push the refund
                // to the gateway that actually took the money.
                var gateway = _gateways.GetGateway(method);
                var gatewayResult = await gateway.RefundAsync(
                    booking.BookingCode, refundAmount, reason ?? label);
                if (!gatewayResult.Ok)
                {
                    // Revert the claim so the booking is refundable again.
                    await Uow.ExecuteSqlRawAsync(
                        "UPDATE Bookings SET Status = 'Confirmed', " +
                        "  CancelReason = @p0, UpdatedAt = @p1 " +
                        "WHERE Id = @p2 AND Status = 'Refunded'",
                        "refund gateway failed, reverted", DateTime.UtcNow, bookingId);
                    await SaveAsync();
                    throw new ConflictException(
                        "Gateway tu choi hoan tien: " + (gatewayResult.Error ?? "loi khong xac dinh."));
                }

                var refundRow = new Payment
                {
                    BookingId = booking.Id,
                    UserId = booking.UserId,
                    Method = method,
                    Amount = booking.PayableAmount,
                    Status = ratio == 1.0
                        ? PaymentStatus.Refunded
                        : (ratio > 0 ? PaymentStatus.PartialRefund : PaymentStatus.Refunded),
                    RefundAmount = refundAmount,
                    RefundedAt = DateTime.UtcNow,
                    // Deterministic, unique per booking → at most ONE refund
                    // row can exist (enforced by IX_Payments_GatewayRef).
                    GatewayRef = $"REFUND-{booking.Id}",
                    RawResponse = Trunc(label)
                };
                _ = Repo.AddAsync(refundRow);

                // Points mirror money: a full refund reverses the whole
                // credit, a partial one reverses its share; a no-show (ratio
                // 0) keeps the points.
                if (ratio > 0 && refundAmount > 0)
                    await _points.ClawbackAsync(booking.UserId, booking.Id, refundAmount,
                        $"refund {label} of " + booking.BookingCode);

                // Restore any points the customer redeemed toward this booking
                // (a full refund of money also gives back the points they spent).
                if (booking.PointsRedeemed > 0)
                    await _points.RestoreAsync(booking.UserId, booking.Id,
                        booking.PointsRedeemed, booking.BookingCode);

                await SaveAsync();
                await _audit.LogAsync(actorId, "payment.refund", "Payment", refundRow.Id.ToString(),
                    null, refundRow, reason ?? label);
                return ToView(refundRow);
            }
            finally
            {
                ReleaseRefundLock(bookingId, refundLock);
            }
        }

        /// <summary>Read payments for a booking — owner or staff only (IDOR guard).</summary>
        public async Task<IReadOnlyList<PaymentView>> GetByBookingAsync(
            int bookingId, int requestingUserId)
        {
            var booking = await Bookings.GetByIdAsync(bookingId);
            if (booking is null) throw new NotFoundException("Booking", bookingId);
            if (booking.UserId != requestingUserId && !IsStaff())
                throw new ConflictException("Khong co quyen xem giao dich thanh toan nay.");

            var list = await Repo.GetByConditionAsync(p => p.BookingId == bookingId);
            return list.Select(ToView).ToList();
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private static string? Trunc(string? s) =>
            string.IsNullOrEmpty(s) ? s : (s.Length > 2000 ? s[..2000] : s);

        private bool IsStaff() =>
            _http.HttpContext?.User.IsInRole("Admin") == true
            || _http.HttpContext?.User.IsInRole("Manager") == true;

        private async Task AuthorizeStaffScope(Booking booking, int actorId)
        {
            var ctx = _http.HttpContext;
            bool isAdmin = ctx?.User.IsInRole("Admin") == true;
            if (isAdmin) return; // global

            var actor = await Repository<User>().GetByIdAsync(actorId);
            if (actor is null || actor.Role != Role.Manager)
                throw new ConflictException("Chỉ Manager/Admin mới thao tác được đơn của khách.");
            if (actor.BranchId is { } bid && bid != booking.BranchId)
                throw new ConflictException(
                    $"Bạn chỉ được xử lý đơn thuộc cơ sở của mình (cơ sở {bid}).");
        }

        private static PaymentMethod ParseMethod(string s) =>
            Enum.TryParse<PaymentMethod>(s, ignoreCase: true, out var m)
                ? m
                : throw new BadRequestException($"Phuong thuc thanh toan '{s}' khong hop le.");

        private static PaymentView ToView(Payment p) => new(
            p.Id, p.BookingId, p.Method.ToString(), p.Amount, p.Status.ToString(),
            p.GatewayRef, p.PaidAt, p.RefundAmount, p.RefundedAt);
    }
}
