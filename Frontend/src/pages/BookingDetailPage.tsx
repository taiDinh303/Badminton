import { useMemo, useState } from "react";
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
import {
  api,
  formatDay,
  formatVnd,
  slotLabelClean,
  type PaymentView,
} from "../lib/api";
import {
  Busy,
  Button,
  Callout,
  Card,
  StatusBadge,
  Pills,
} from "../components/ui";
import { isStaff } from "../store/auth";
import { useAuth } from "../store/auth";

/** Deterministic visual tile from the server QR token (display-only, docs 03 §3). */
function QrTile({ code }: { code: string }) {
  const n = 9;
  let seed = 0;
  for (let i = 0; i < code.length; i++) seed = (seed * 31 + code.charCodeAt(i)) >>> 0;
  const cells: boolean[] = [];
  for (let i = 0; i < n * n; i++) {
    seed = (seed * 1103515245 + 12345) & 0x7fffffff;
    cells.push((seed >> 13) % 100 < 52);
  }
  return (
    <div className="inline-block rounded-lg border border-border bg-card p-2">
      <div
        style={{ display: "grid", gridTemplateColumns: `repeat(${n}, 10px)`, gap: 2 }}
        aria-label={`QR mã ${code}`}
      >
        {cells.map((on, i) => (
          <span key={i} className={on ? "h-2.5 w-2.5 bg-fg" : "h-2.5 w-2.5"} />
        ))}
      </div>
      <p className="mt-2 max-w-28 break-all text-center text-[10px] font-semibold tnum">
        {code}
      </p>
    </div>
  );
}

const ONLINE_METHODS = ["VnPay", "MoMo", "Zalopay"] as const;
type Method = (typeof ONLINE_METHODS)[number] | "Cash";

export default function BookingDetailPage() {
  const { id } = useParams();
  const bookingId = Number(id);
  const user = useAuth((s) => s.user);
  const staff = isStaff(user);
  const qc = useQueryClient();

  const [method, setMethod] = useState<Method>("VnPay");
  const [copied, setCopied] = useState(false);

  const { data: b, isFetching, refetch } = useQuery({
    queryKey: ["booking", bookingId],
    queryFn: () => api.booking.getById(bookingId),
    enabled: !Number.isNaN(bookingId),
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

  const cancel = useMutation({
    mutationFn: () => api.booking.cancel(bookingId),
    onSuccess: () => {
      refetch();
      qc.invalidateQueries({ queryKey: ["my-bookings"] });
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
    mutationFn: (rating: number) =>
      api.reviews.create({ bookingId, rating }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["reviews"] }),
  });

  const [rating, setRating] = useState(0);

  const showQr = b?.status === "Confirmed" || b?.status === "Completed";
  const canPay = b?.status === "Pending" && !staff;
  const p = b?.price;

  const paid = useMemo(
    () => pays?.some((x) => x.status === "Success"),
    [pays],
  );

  if (isFetching && !b) return <div className="p-4"><Busy label="Đang tải đơn…" /></div>;
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
          {showQr && <QrTile code={b.qrToken ?? b.bookingCode} />}
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
            <div className="flex justify-between"><dt className="text-muted">Giá gốc</dt><dd>{formatVnd(p.basePrice)}</dd></div>
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
            {ONLINE_METHODS.map((m) => (
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
                {m}
              </button>
            ))}
          </div>

          <Button
            variant="accent"
            className="mt-3 w-full"
            disabled={pay.isPending}
            onClick={() => pay.mutate()}
          >
            {pay.isPending ? (
              <><Loader2 className="h-4 w-4 animate-spin" /> Đang tạo thanh toán…</>
            ) : (
              <>Thanh toán bằng {method}</>
            )}
          </Button>

          {pay.isError && (
            <Callout tone="error" className="mt-2">
              Thanh toán chưa hoàn tất. Thử lại hoặc hủy để giải phóng slot.
            </Callout>
          )}
        </Card>
      )}

      {/* ── Review: only after Completed + one per booking (docs 03 §7) ── */}
      {b.status === "Completed" && !staff && (
        <Card className="mt-4">
          <p className="mb-2 flex items-center gap-1 text-sm font-semibold">
            <Star className="h-4 w-4 text-accent" /> Đánh giá lần tập
          </p>
          <div className="flex items-center gap-3">
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
              onClick={() => review.mutate(rating)}
            >
              {review.isPending ? "Đang gửi…" : "Gửi đánh giá"}
            </Button>
          </div>
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
