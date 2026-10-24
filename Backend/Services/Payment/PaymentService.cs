using Contract.Repositories.Entity;
using Contract.Repositories.UnitOfWork;
using Contract.Services.Audit;
using Contract.Services.Notify;
using Contract.Services.Payment;
using Contract.Services.Points;
using Contract.Services.Voucher;
using Badminton.Core.Base;
using Badminton.Core.Domain;
using Badminton.Core.Enums;
using Badminton.Core.Utils;
using Badminton.Core.Security;
using ModelViews.Payment;

namespace Services
{
    public sealed class PaymentService : BaseService, IPaymentService
    {
        private readonly IPaymentGatewayFactory _gateways;
        private readonly IPointService _points;
        private readonly IAuditService _audit;
        private readonly INotifyService _notify;
        private readonly OnlineRefundService? _onlineRefunds;
        private readonly IVoucherService _vouchersForRefund;

        public PaymentService(IUnitOfWork uow, ICurrentUser currentUser,
            IPaymentGatewayFactory gateways, IPointService points,
            IAuditService audit, INotifyService notify, IVoucherService vouchersForRefund, OnlineRefundService? onlineRefunds = null)
            : base(uow, currentUser)
        {
            _gateways = gateways;
            _points = points;
            _audit = audit;
            _notify = notify;
            _onlineRefunds = onlineRefunds;
            _vouchersForRefund = vouchersForRefund;
        }

        private IGenericRepository<Payment> Repo => Repository<Payment>();
        private IGenericRepository<Booking> Bookings => Repository<Booking>();

        private static readonly IReadOnlyDictionary<string, string> EmptyHeaders
            = new Dictionary<string, string>();

        public Task<PaymentView> ReconcileRefundAsync(int paymentId, int actorId) => _onlineRefunds is not null
            ? _onlineRefunds.RequestAsync(paymentId, actorId, "Hoàn khoản thu cần đối soát", lateReceipt: true)
            : throw new ConflictException("ONLINE_REFUND_DISABLED: Chưa cấu hình dịch vụ hoàn tiền online.");

        public async Task<PaymentInitResponse> InitAsync(InitPaymentRequest request, int userId)
        {
            var method = ParseMethod(request.Method);
            var gateway = _gateways.GetGateway(method);
            Payment payment = null!;
            Booking booking = null!;
            var created = false;
            await Uow.RunInTransactionAsync(async () =>
            {
                await Uow.LockBookingRequestAsync($"payment-init:{request.BookingId}");
                booking = (await Bookings.GetByConditionAsync(b => b.Id == request.BookingId && b.DeletedAt == null)).SingleOrDefault()
                    ?? throw new NotFoundException("Booking", request.BookingId);
                if (booking.UserId != userId) await AuthorizeStaffScope(booking, userId);
                if (booking.Status != BookingStatus.Pending || booking.PendingExpiresAt is not { } expiry || expiry <= DateTime.UtcNow)
                    throw new ConflictException("Đơn không còn trong thời hạn thanh toán.");
                if (method is PaymentMethod.Cash or PaymentMethod.QR) return;
                if (booking.PayableAmount <= 0) throw new BadRequestException("Đơn 0đ không cần thanh toán online.");
                if (!gateway.IsSimulation && (request.ReturnUrl?.Length > 2048 || !Uri.TryCreate(request.ReturnUrl, UriKind.Absolute, out var returnUri)
                    || (returnUri.Scheme != "https" && !(returnUri.Scheme == "http" && returnUri.IsLoopback)) || !string.IsNullOrEmpty(returnUri.UserInfo)))
                    throw new BadRequestException("ReturnUrl phải là HTTPS tuyệt đối (HTTP chỉ cho loopback).");
                var attempts = await Repo.GetByConditionAsync(p => p.BookingId == booking.Id
                    && p.Method != PaymentMethod.Cash && p.Method != PaymentMethod.QR);
                if (attempts.Count > 0)
                {
                    payment = attempts.OrderByDescending(p => p.Id).First();
                    if (payment.Method != method || payment.ReturnUrl != request.ReturnUrl || payment.Status != PaymentStatus.Pending
                        || string.IsNullOrWhiteSpace(payment.GatewayUrl))
                        throw new ConflictException("Đã có giao dịch online; cần kiểm tra trạng thái/đối soát trước khi tạo giao dịch khác.");
                    return;
                }
                payment = new Payment { BookingId = booking.Id, UserId = booking.UserId, Amount = booking.PayableAmount,
                    Method = method, GatewayRef = HanoiTime.HanoiNow().ToString("yyMMdd") + "_" + Guid.NewGuid().ToString("N"),
                    ReturnUrl = request.ReturnUrl, ReconciliationRequired = true, ReconciliationReason = "initialization_uncertain" };
                await Repo.AddAsync(payment);
                await SaveAsync();
                created = true;
            });
            if (method is PaymentMethod.Cash or PaymentMethod.QR)
                return new(method.ToString(), null, null, booking.BookingCode);
            if (created)
            {
                PaymentInitResult init;
                try { init = await gateway.InitAsync(payment.GatewayRef!, payment.Amount, request.ReturnUrl ?? "/", default); }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or System.Text.Json.JsonException or InvalidOperationException)
                { throw new ConflictException("Khởi tạo thanh toán chưa rõ kết quả. Không gửi lại; giao dịch đã lưu để đối soát."); }
                if (!init.Ok || init.GatewayRef != payment.GatewayRef || string.IsNullOrWhiteSpace(init.GatewayUrl) || init.GatewayUrl.Length > 2048)
                    throw new ConflictException("Cổng chưa xác nhận đúng giao dịch. Giao dịch đã lưu để đối soát, không tạo lại.");
                await Uow.RunInTransactionAsync(async () => {
                    var transactionDate = method == PaymentMethod.VnPay && Uri.TryCreate(init.GatewayUrl, UriKind.Absolute, out var checkout)
                        ? System.Web.HttpUtility.ParseQueryString(checkout.Query)["vnp_CreateDate"] : null;
                    await Uow.Payments.RecordCheckoutAsync(payment.Id, init.GatewayUrl, transactionDate);
                    await Uow.Payments.ClearInitializationReviewAsync(payment.Id);
                });
                payment.GatewayUrl = init.GatewayUrl;
            }
            return new(method.ToString(), payment.GatewayUrl, payment.GatewayRef, booking.BookingCode);
        }

        // ── Webhook: verified → idempotent → atomic ────────────────────────
        public async Task<WebhookResult> HandleWebhookAsync(string provider, PaymentWebhookRequest raw)
        {
            if (string.IsNullOrWhiteSpace(raw.GatewayRef))
                return WebhookResult.Reject("webhook thieu gateway_ref.");

            IPaymentGateway gateway;
            try { gateway = _gateways.GetGatewayByProvider(provider); }
            catch (BadRequestException ex) { return WebhookResult.Reject(ex.Message); }

            if (string.IsNullOrEmpty(raw.RawResponse) || raw.RawResponse.Length > 65536)
                return WebhookResult.Reject(
                    "webhook thieu raw payload tu gateway, khong the xac minh.");

            var verified = await gateway.VerifyWebhookAsync(
                new RawWebhook(provider, raw.GatewayRef, raw.RawResponse,
                    raw.Status, EmptyHeaders));
            if (!verified.Verified)
                return WebhookResult.Reject(verified.Error ?? "webhook chua duoc xac minh ky hieu.");

            var known = await Repo.GetByConditionAsync(
                p => p.GatewayRef == raw.GatewayRef);
            if (known.Count == 0)
                return WebhookResult.Reject("gateway_ref khong ton tai trong he thong.");
            var recorded = known.Single();
            if (recorded.Method != gateway.Method || !string.Equals(verified.GatewayRef, recorded.GatewayRef, StringComparison.Ordinal)
                || (!gateway.IsSimulation && (verified.Amount != recorded.Amount || verified.Currency != "VND")))
                return WebhookResult.Reject("Webhook không khớp provider, đơn hoặc số tiền đã lưu.");
            if (known.First().Status == PaymentStatus.Success)
                return WebhookResult.Duplicate("gateway_ref da duoc xu ly truoc do.");

            if (!verified.Success)
            {
                await Uow.Payments.FailAsync(raw.GatewayRef);
                await SaveAsync();
                return WebhookResult.Reject("gateway bao loi, don vao tung pending/failed.");
            }

            var refNo = raw.GatewayRef;
            int confirmed = 0;
            int bookingId = 0;
            bool requiresReview = false;

            await Uow.RunInTransactionAsync(async () =>
            {
                string rawBody = Trunc(raw.RawResponse) ?? string.Empty;

                int claimed = await Uow.Payments.SucceedAsync(refNo, rawBody, DateTime.UtcNow);
                if (claimed == 0) return; // lost a same-instant race → skip

                var p = (await Repo.GetByConditionAsync(x => x.GatewayRef == refNo)).First();
                p.ProviderTransactionId = verified.TransactionId;
                p.ProviderTransactionDate = verified.TransactionDate ?? p.ProviderTransactionDate;
                Repo.Update(p);
                await SaveAsync();
                var booking = await Bookings.GetByIdAsync(p.BookingId);
                await Uow.Payments.ClearInitializationReviewAsync(p.Id);
                if (booking is null || booking.Status != BookingStatus.Pending || booking.PendingExpiresAt is not { } receiptDeadline || receiptDeadline <= DateTime.UtcNow)
                {
                    requiresReview = true;
                    await Uow.Payments.RequireReviewAsync(p.Id, booking?.Status is BookingStatus.Confirmed or BookingStatus.Completed
                        ? "additional_receipt" : "late_receipt");
                    return; // preserve the receipt and terminal booking; expose it to staff reconciliation.
                }

                bookingId = booking.Id;

                confirmed = await Uow.Bookings.ConfirmAsync(bookingId, GenerateQrToken(), DateTime.UtcNow);
                if (confirmed > 0) await _notify.QueueBookingEventAsync(NotifyEvent.BookingConfirmed, bookingId);
                else {
                    requiresReview = true;
                    await Uow.Payments.RequireReviewAsync(p.Id, "confirmation_conflict");
                }
                await SaveAsync();
            });

            if (confirmed > 0)
                await _notify.NotifyBookingConfirmedAsync(bookingId);

            if (requiresReview) return WebhookResult.Applied("Đã ghi nhận tiền; cần đối soát, không xác nhận lại đơn.");
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
            if (booking.DeletedAt.HasValue || booking.PendingExpiresAt is not { } expiry || expiry <= DateTime.UtcNow)
                throw new ConflictException("Đơn đã hết thời hạn giữ chỗ, không thể xác nhận thanh toán tại quầy.");

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
                confirmed = await Uow.Bookings.ConfirmAsync(bookingId, GenerateQrToken(), DateTime.UtcNow);
                if (confirmed == 0)
                    throw new ConflictException(
                        $"Đơn {booking.BookingCode} đã hết hạn hoặc vừa thay đổi trạng thái. Vui lòng tải lại.");

                await Repo.AddAsync(payment);
                await _notify.QueueBookingEventAsync(NotifyEvent.BookingConfirmed, bookingId);
                await SaveAsync();
            });

            // FEAT 1: post-confirm notification.
            if (confirmed > 0)
                await _notify.NotifyBookingConfirmedAsync(bookingId);

            await _audit.LogAsync(actorId, "payment.on-site-confirm", "Payment",
                payment.Id.ToString(), null,
                new { payment.Method, payment.Amount, payment.Status, payment.GatewayRef, payment.PaidAt },
                note);
            return ToView(payment);
        }

        public Task<PaymentView> RefundAsync(int bookingId, int actorId, string? reason = null) =>
            RefundCoreAsync(bookingId, actorId, reason, cancel: false, maintenance: false);

        public Task<PaymentView> RefundForMaintenanceAsync(int bookingId, int actorId, string? reason = null) =>
            RefundCoreAsync(bookingId, actorId, reason, cancel: false, maintenance: true);

        public Task<PaymentView> CancelRefundAsync(int bookingId, int actorId, string? reason = null) =>
            RefundCoreAsync(bookingId, actorId, reason, cancel: true, maintenance: false);

        private async Task<PaymentView> RefundCoreAsync(int bookingId, int actorId, string? reason,
            bool cancel, bool maintenance)
        {
            var receipt = (await Repo.GetByConditionAsync(p => p.BookingId == bookingId && p.Status == PaymentStatus.Success)).FirstOrDefault();
            if (receipt is not null && receipt.Method is not (PaymentMethod.Cash or PaymentMethod.QR)
                && !_gateways.GetGateway(receipt.Method).IsSimulation && _onlineRefunds is not null)
                return await _onlineRefunds.RequestAsync(receipt.Id, actorId, reason, cancel, maintenance);
            PaymentView result = null!;
            await Uow.RunInTransactionAsync(async () =>
            {
                var booking = await Bookings.GetByIdAsync(bookingId)
                    ?? throw new NotFoundException("Booking", bookingId);
                if (!cancel || booking.UserId != actorId)
                    await AuthorizeStaffScope(booking, actorId);

                var existing = await FindRefundAsync(bookingId);
                if (existing is not null) { result = ToView(existing); return; }
                if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Completed))
                    throw new ConflictException("Chỉ hoàn tiền đơn đã thanh toán.");
                var slot = await Repository<TimeSlot>().GetByIdAsync(booking.SlotId)
                    ?? throw new ConflictException("Khung giờ của đơn bị mất.");
                var (ratio, label) = maintenance
                    ? (1.0, "Hoàn toàn phần do sân bảo trì/đóng")
                    : RefundPolicy.Tier(HanoiTime.HanoiNow(), HanoiTime.SlotStart(booking.Date, slot.StartTime));
                var amount = (int)(booking.PayableAmount * ratio);
                var original = (await Repo.GetByConditionAsync(p => p.BookingId == bookingId
                    && p.Status == PaymentStatus.Success)).FirstOrDefault();
                if (original is null || original.Amount != booking.PayableAmount)
                    throw new ConflictException("Không có giao dịch thu tiền hợp lệ để hoàn. Cần đối soát trước.");
                var method = original.Method;

                var claimed = await Uow.Bookings.ClaimRefundAsync(bookingId, cancel,
                    cancel ? "Huy da thanh toan: " + label : label, DateTime.UtcNow);
                if (claimed == 0)
                {
                    var winner = await FindRefundAsync(bookingId);
                    result = winner is not null ? ToView(winner)
                        : throw new ConflictException("Đơn vừa thay đổi trạng thái. Vui lòng tải lại.");
                    return;
                }
                if (amount > 0)
                {
                    var gateway = _gateways.GetGateway(method);
                    var refund = await gateway.RefundAsync(booking.BookingCode, amount, reason ?? label);
                    if (!refund.Ok || refund.Amount != amount) throw new ConflictException("Gateway từ chối hoàn tiền: " + refund.Error);
                }
                var row = new Payment
                {
                    BookingId = booking.Id, UserId = booking.UserId, Method = method,
                    Amount = booking.PayableAmount, RefundAmount = amount,
                    Status = ratio > 0 && ratio < 1 ? PaymentStatus.PartialRefund : PaymentStatus.Refunded,
                    RefundedAt = DateTime.UtcNow,
                    GatewayRef = $"{(cancel ? "CANCEL-REFUND" : "REFUND")}-{booking.Id}",
                    RawResponse = Trunc(label)
                };
                await Repo.AddAsync(row);
                if (amount > 0 && await _points.HasEarnedRowAsync(booking.Id))
                    await _points.ClawbackAsync(booking.UserId, booking.Id, amount, $"refund {label} of {booking.BookingCode}");
                if (ratio == 1 && booking.PointsRedeemed > 0)
                    await _points.RestoreAsync(booking.UserId, booking.Id, booking.PointsRedeemed, booking.BookingCode);
                if ((cancel || maintenance) && booking.VoucherRedemptionId is { } redemptionId)
                    await _vouchersForRefund.ReleaseVoucherAsync(redemptionId);
                await SaveAsync();
                await _audit.LogAsync(actorId, cancel ? "booking.cancel-refund" : "payment.refund",
                    "Payment", row.Id.ToString(), null, row, reason ?? label);
                result = ToView(row);
            });
            return result;
        }

        private async Task<Payment?> FindRefundAsync(int bookingId) =>
            (await Repo.GetByConditionAsync(p => p.BookingId == bookingId &&
                (p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartialRefund))).FirstOrDefault();

        public async Task<IReadOnlyList<PaymentView>> GetByBookingAsync(
            int bookingId, int requestingUserId)
        {
            var booking = await Bookings.GetByIdAsync(bookingId);
            if (booking is null) throw new NotFoundException("Booking", bookingId);
            if (booking.UserId != requestingUserId && !IsStaff())
                throw new ConflictException("Khong co quyen xem giao dich thanh toan nay.");

            if (booking.UserId != requestingUserId)
                await AuthorizeStaffBranchAsync(booking.BranchId, requestingUserId);
            var list = await Repo.GetByConditionAsync(p => p.BookingId == bookingId);
            return list.Select(ToView).ToList();
        }

        public async Task<(IReadOnlyList<PaymentReconciliationView> Items, int TotalCount)> GetReconciliationAsync(int? branchId, Page page)
        {
            var branch = await ResolveStaffBranchAsync(branchId);
            var (items, total) = await Repo.GetPagedAsync(p => p.ReconciliationRequired
                && (!branch.HasValue || p.Booking!.BranchId == branch.Value), p => p.CreatedAt, OrderDirection.Asc, page);
            return (items.Select(p => new PaymentReconciliationView(ToView(p), p.ReconciliationReason, p.CreatedAt)).ToList(), total);
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private static string GenerateQrToken()
        {
            var bytes = new byte[32];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string? Trunc(string? s) =>
            string.IsNullOrEmpty(s) ? s : (s.Length > 2000 ? s[..2000] : s);

        private bool IsStaff() =>
            _currentUser.Principal.IsInRole("Admin") == true
            || _currentUser.Principal.IsInRole("Manager") == true;

        private Task AuthorizeStaffScope(Booking booking, int actorId) =>
            AuthorizeStaffBranchAsync(booking.BranchId, actorId);

        private static PaymentMethod ParseMethod(string s) =>
            Enum.TryParse<PaymentMethod>(s, ignoreCase: true, out var m)
                ? m
                : throw new BadRequestException($"Phuong thuc thanh toan '{s}' khong hop le.");

        private static PaymentView ToView(Payment p) => new(
            p.Id, p.BookingId, p.Method.ToString(), p.Amount, p.Status.ToString(),
            p.GatewayRef, p.PaidAt, p.RefundAmount, p.RefundedAt);
    }
}
