import { useEffect, useMemo, useState } from "react";
import QRCode from "qrcode";
import { useParams, Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft,
  CreditCard,
  QrCode,
  Ticket,
  Star,
  CheckCircle2,
  Loader2,
  Wallet,
  Copy,
  Ban,
} from "lucide-react";
import { api, type PaymentView } from "../../../services/api";
import { formatDay, formatVnd, slotLabelClean } from "../../../utils/format";
import {
  Busy,
  Button,
  Callout,
  Card,
  StatusBadge,
  Pills,
} from "../../../components/ui";
import { isStaff } from "../../../store/auth";
import { useAuth } from "../../../store/auth";

/** The QR contains the server-issued token accepted by /checkin. */
function QrTile({ code }: { code: string }) {
  const [src, setSrc] = useState("");
  useEffect(() => {
    let active = true;
    QRCode.toDataURL(code, { width: 180, margin: 4, errorCorrectionLevel: "M" })
      .then((value) => { if (active) setSrc(value); }).catch(() => { if (active) setSrc(""); });
    return () => { active = false; };
  }, [code]);
  return <div className="max-w-48 rounded-lg border border-border p-2">
    {src && <img src={src} alt="QR token check-in" width={180} height={180} />}
    <p className="break-all text-center text-xs font-mono">{code}</p>
    <Button variant="ghost" size="xs" onClick={() => navigator.clipboard.writeText(code)}>Sao chép token</Button>
  </div>;
}

const PAYMENT_METHODS = ["Cash", "QR", "VnPay", "MoMo", "Zalopay"] as const;
type Method = (typeof PAYMENT_METHODS)[number];
const methodLabel = (method: Method) => method === "Cash" ? "Tiền mặt tại sân" : method === "QR" ? "QR tại quầy" : method;

export default function BookingDetailPage() {
  const { id } = useParams();
  const bookingId = Number(id);
  const user = useAuth((s) => s.user);
  const staff = isStaff(user);
  const qc = useQueryClient();

  const [method, setMethod] = useState<Method>("Cash");
  const methods = useQuery({ queryKey: ["payment-methods"], queryFn: api.payments.methods });
  const availableMethods = PAYMENT_METHODS.filter((m) => methods.data?.includes(m));
  const [copied, setCopied] = useState(false);

  const { data: b, isLoading, refetch } = useQuery({
    queryKey: ["booking", bookingId],
    queryFn: () => api.booking.getById(bookingId),
    enabled: !Number.isNaN(bookingId),
    staleTime: 0,
    // While the order is Pending we poll so a successful gateway callback is
    // picked up without the user refreshing (docs 03 §3).
    refetchInterval: (q) =>
      q.state.data?.status === "Pending" && !staff ? 5000 : false,
  });

  const { data: pays } = useQuery({
    queryKey: ["payments", bookingId],
    queryFn: () => api.payments.forBooking(bookingId),
    enabled: !Number.isNaN(bookingId),
  });

  const reviews = useQuery({
    queryKey: ["reviews", "court", b?.courtId],
    queryFn: () => api.reviews.forCourt(b!.courtId),
    enabled: !!b && b.status === "Completed",
  });
  const existingReview = reviews.data?.find((r) => r.bookingId === bookingId);

  const cancel = useMutation({
    mutationFn: () => api.booking.cancel(bookingId),
    onSuccess: () => {
      refetch();
      qc.invalidateQueries({ queryKey: ["my-bookings"] });
      void qc.invalidateQueries({ queryKey: ["account"] });
      void qc.invalidateQueries({ queryKey: ["availability"] });
    },
  });

  // Real online payment: init → gateway URL (redirect) → poll until Confirmed.
  const pay = useMutation({
    mutationFn: () =>
      api.payments.init({
        bookingId,
        method: method === "Cash" ? "cash" : method,
        returnUrl: window.location.href,
      }),
    onSuccess: (res) => {
      // Online gateways hand back a checkout URL to open. The mock gateway in
      // dev returns https://mock-gateway.local/... which we surface (and, when a
      // real URL exists, the browser follows it).
      if (res.gatewayUrl) {
        window.open(res.gatewayUrl, "_blank", "noopener,noreferrer");
      }
      // Kick a refresh so the poll loop / status reflects the new payment row.
      qc.invalidateQueries({ queryKey: ["payments", bookingId] });
      refetch();
    },
  });

  const review = useMutation({
    mutationFn: () => api.reviews.create({ bookingId, rating, comment: comment.trim() || null }),
    onSuccess: (created) => {
      qc.setQueryData(["reviews", "court", b!.courtId], [...(reviews.data ?? []), created]);
      void qc.invalidateQueries({ queryKey: ["reviews"] });
      void qc.invalidateQueries({ queryKey: ["courts"] });
    },
  });

  const [rating, setRating] = useState(0);
  const [comment, setComment] = useState("");
  useEffect(() => { setRating(0); setComment(""); }, [bookingId]);

  const showQr = b?.status === "Confirmed" || b?.status === "Completed";
  const canPay = b?.status === "Pending" && !staff;
  const p = b?.price;

  const paid = useMemo(
    () => pays?.some((x) => x.status === "Success"),
    [pays],
  );

  if (isLoading && !b) return <div className="p-4"><Busy label="Đang tải đơn…" /></div>;
  if (!b)
    return (
      <div className="p-4">
        <Callout tone="error">Không tìm thấy đơn.</Callout>
      </div>
    );

  async function copyCode() {
    try {
      await navigator.clipboard.writeText(b!.bookingCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      /* clipboard blocked */
    }
  }

  return (
    <div className="mx-auto max-w-2xl p-4 md:p-6">
      <Link
        to="/bookings"
        className="mb-4 inline-flex items-center gap-1 text-sm text-muted transition-colors hover:text-fg"
      >
        <ArrowLeft className="h-4 w-4" /> Đơn của tôi
      </Link>

      {/* Status hero */}
      <Card className="mt-4 overflow-hidden">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border bg-bg/50 p-4">
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-lg font-bold tnum">{b.bookingCode}</h1>
              <button
                type="button"
                onClick={copyCode}
                aria-label="Sao chép mã đặt"
                className="flex h-7 w-7 items-center justify-center rounded-lg text-muted transition-colors hover:bg-bg hover:text-fg"
              >
                {copied ? <CheckCircle2 className="h-4 w-4 text-primary" /> : <Copy className="h-3.5 w-3.5" />}
              </button>
            </div>
            <p className="mt-0.5 text-xs text-muted">Mã đặt sân cầu lông</p>
          </div>
          <StatusBadge status={b.status} />
        </div>
        <div className="flex flex-wrap items-center justify-between gap-4 p-4">
          <div className="min-w-0">
            <p className="text-base font-bold">{b.courtName}</p>
            <p className="mt-1 text-sm text-muted tnum">
              {formatDay(b.date)} · {slotLabelClean(b.slotLabel)} · {b.playerCount} người
            </p>
          </div>
          {showQr && b.qrToken && <QrTile code={b.qrToken} />}
        </div>
        {showQr && (
          <p className="flex items-center gap-1 border-t border-border bg-bg/40 px-4 py-2.5 text-xs text-muted">
            <QrCode className="h-3.5 w-3.5" /> Xuất trình mã này để nhân viên check-in.
          </p>
        )}
        {b.checkedInAt && (
          <p className="flex items-center gap-1 border-t border-border px-4 py-2.5 text-xs text-state-success">
            <CheckCircle2 className="h-3.5 w-3.5" /> Đã check-in lúc{" "}
            {new Date(b.checkedInAt).toLocaleString("vi-VN")}
          </p>
        )}
      </Card>

      {/* Price breakdown (docs 07 §4: show every line clearly) */}
      {p && (
        <Card className="mt-4">
          <p className="mb-2 flex items-center gap-1 text-sm font-semibold">
            <Ticket className="h-4 w-4 text-primary" /> Chi tiết giá
          </p>
          <dl className="space-y-1 text-sm tnum">
            <div className="flex justify-between"><dt className="text-muted">Giá sân (gồm phụ thu)</dt><dd>{formatVnd(p.basePrice)}</dd></div>
            <div className="flex justify-between"><dt className="text-muted">Phụ thu cuối tuần</dt><dd>{formatVnd(p.weekendSurcharge)}</dd></div>
            <div className="flex justify-between"><dt className="text-muted">Giảm voucher</dt><dd>−{formatVnd(p.voucherDiscount)}</dd></div>
            <div className="flex justify-between"><dt className="text-muted">Giảm điểm</dt><dd>−{formatVnd(p.pointsDiscount)}</dd></div>
            <div className="flex justify-between border-t border-border pt-2 text-base font-bold"><dt>Tổng thanh toán</dt><dd>{formatVnd(p.payable)}</dd></div>
          </dl>
        </Card>
      )}

      {/* ── Payment panel (docs 03 §3: init → gateway → poll) ── */}
      {canPay && (
        <Card className="mt-4">
          <p className="mb-1 flex items-center gap-1 text-sm font-semibold">
            <CreditCard className="h-4 w-4 text-primary" /> Thanh toán
          </p>
          <p className="mb-3 flex items-center gap-1 text-xs text-muted">
            <Wallet className="h-3.5 w-3.5" />
            {paid
              ? "Đã có giao dịch thanh toán, vui lòng đối chiếu."
              : "Đang giữ sân 10 phút. Hoàn tất thanh toán để giữ khung giờ."}
          </p>

          <div className="flex flex-wrap gap-2">
            {availableMethods.map((m) => (
              <button
                key={m}
                type="button"
                onClick={() => setMethod(m)}
                aria-pressed={method === m}
                className={
                  "rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors cursor-pointer " +
                  (method === m
                    ? "border-primary bg-primary/10 text-primary"
                    : "border-border bg-card text-muted hover:text-fg")
                }
              >
                {methodLabel(m)}
              </button>
            ))}
          </div>

          <Button
            variant="accent"
            className="mt-3 w-full"
            disabled={pay.isPending || !methods.data?.includes(method)}
            onClick={() => pay.mutate()}
          >
            {pay.isPending ? (
              <><Loader2 className="h-4 w-4 animate-spin" /> Đang tạo thanh toán…</>
            ) : (
              <>{method === "Cash" || method === "QR" ? "Hướng dẫn thanh toán tại sân" : `Thanh toán bằng ${methodLabel(method)}`}</>
            )}
          </Button>

          {pay.isSuccess && (method === "Cash" || method === "QR") && (
            <Callout tone="info" className="mt-2">Vui lòng thanh toán tại quầy và nhờ nhân viên xác nhận trước khi hết thời gian giữ chỗ. Đơn chỉ được xác nhận sau khi nhân viên ghi nhận thanh toán.</Callout>
          )}
          {methods.isError && <Callout tone="error" className="mt-2">Không tải được phương thức thanh toán. Vui lòng tải lại trang.</Callout>}

          {pay.isError && (
            <Callout tone="error" className="mt-2">
              Thanh toán chưa hoàn tất. Thử lại hoặc hủy để giải phóng slot.
            </Callout>
          )}
        </Card>
      )}

      {/* ── Review: only after Completed + one per booking (docs 03 §7) ── */}
      {b.status === "Completed" && (
        <Card className="mt-4">
          <p className="mb-2 flex items-center gap-1 text-sm font-semibold">
            <Star className="h-4 w-4 text-accent" /> Đánh giá lần tập
          </p>
          {reviews.isLoading ? <Busy label="Đang tải đánh giá…" /> : reviews.isError ? (
            <Callout tone="error">Không tải được đánh giá. <Button size="sm" onClick={() => reviews.refetch()}>Thử lại</Button></Callout>
          ) : existingReview ? (
            <div><p aria-label={`${existingReview.rating}/5 sao`} className="font-semibold text-primary">{existingReview.rating}/5 sao</p><p className="mt-2 whitespace-pre-wrap text-sm">{existingReview.comment}</p><p className="mt-2 text-xs text-muted">Đơn này đã được đánh giá.</p></div>
          ) : b.userId === user?.id ? <>
          <div className="flex flex-wrap items-center gap-3">
            <Pills
              options={[
                { key: "1", label: "1" },
                { key: "2", label: "2" },
                { key: "3", label: "3" },
                { key: "4", label: "4" },
                { key: "5", label: "5" },
              ]}
              value={(rating ? String(rating) : "0") as "1" | "2" | "3" | "4" | "5"}
              onChange={(v) => setRating(Number(v))}
              ariaLabel="Chọn số sao"
            />
            <Button
              variant="ghost"
              size="sm"
              disabled={rating === 0 || review.isPending}
              onClick={() => review.mutate()}
            >
              {review.isPending ? "Đang gửi…" : "Gửi đánh giá"}
            </Button>
          </div>
          <label className="mt-3 block text-sm">Nhận xét
            <textarea aria-label="Nhận xét" maxLength={2000} rows={3} value={comment} onChange={(e) => setComment(e.target.value)} placeholder="Chia sẻ trải nghiệm của bạn tại sân…" className="mt-1 w-full rounded-lg border border-border bg-card p-3 text-fg" />
          </label>
          {review.isSuccess && (
            <Callout tone="success" className="mt-2">Cảm ơn bạn đã đánh giá!</Callout>
          )}
          {review.isError && (
            <Callout tone="error" className="mt-2">
              {review.error instanceof Error
                ? review.error.message
                : "Không gửi được đánh giá."}
            </Callout>
          )}
          </> : <p className="text-sm text-muted">Chỉ người đặt sân mới được gửi đánh giá cho đơn này.</p>}
        </Card>
      )}

      {/* ── Cancel / staff lifecycle ── */}
      {b.status === "Pending" && !staff && (
        <div className="mt-4">
          <Button
            variant="danger"
            className="w-full"
            onClick={() => cancel.mutate()}
            disabled={cancel.isPending}
          >
            <Ban className="h-4 w-4" />
            {cancel.isPending ? "Đang hủy…" : "Huỷ đặt chỗ"}
          </Button>
          {cancel.isError && (
            <Callout tone="error" className="mt-2">
              {cancel.error instanceof Error ? cancel.error.message : "Không hủy được."}
            </Callout>
          )}
        </div>
      )}

      {b.cancelReason && b.status === "Cancelled" && (
        <Callout tone="info" className="mt-4">Lý do hủy: {b.cancelReason}</Callout>
      )}

      {/* ── Payment history (docs 07 §4: show every line clearly) ── */}
      {pays && pays.length > 0 && (
        <Card className="mt-4">
          <p className="mb-2 text-sm font-semibold">Giao dịch</p>
          <div className="space-y-2">
            {pays.map((tx: PaymentView) => (
              <div key={tx.id} className="flex items-center justify-between text-sm">
                <div className="flex items-center gap-2">
                  <StatusBadge status={tx.status} size="sm" />
                  <span className="text-muted tnum">
                    {tx.method} · {formatVnd(tx.amount)}
                    {tx.status === "Refunded" && tx.refundAmount > 0 && (
                      <span className="text-state-success"> · hoàn {formatVnd(tx.refundAmount)}</span>
                    )}
                  </span>
                </div>
                {(tx.paidAt ?? tx.refundedAt) && (
                  <span className="text-xs text-muted tnum">
                    {new Date(tx.paidAt ?? tx.refundedAt!).toLocaleString("vi-VN")}
                  </span>
                )}
              </div>
            ))}
          </div>
        </Card>
      )}
    </div>
  );
}
