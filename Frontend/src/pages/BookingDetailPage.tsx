import { useParams, Link } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import { ArrowLeft, CreditCard, QrCode, Ticket } from "lucide-react";
import { api, formatDay, formatVnd } from "../lib/api";
import { Busy, Button, Callout, Card, StatusBadge } from "../components/ui";
import { isStaff } from "../store/auth";
import { useAuth } from "../store/auth";

/** Deterministic visual QR-like tile from the booking code (display-only). */
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
      <p className="mt-2 text-center text-xs font-semibold tnum">{code}</p>
    </div>
  );
}

/**
 * Chi tiết đặt chỗ (docs 07 §4): mã đặt, QR, trạng thái, sân/giờ,
 * phân biệt khoản giá, thanh toán, hủy. QR chỉ hiện khi đã xác nhận hợp lệ.
 */
export default function BookingDetailPage() {
  const { id } = useParams();
  const bookingId = Number(id);
  const user = useAuth((s) => s.user);
  const staff = isStaff(user);

  const { data: b, isFetching } = useQuery({
    queryKey: ["booking", bookingId],
    queryFn: () => api.booking.getById(bookingId),
    enabled: !Number.isNaN(bookingId),
  });

  const { data: pays } = useQuery({
    queryKey: ["payments", bookingId],
    queryFn: () => api.payments.forBooking(bookingId),
    enabled: !Number.isNaN(bookingId),
  });

  const cancel = useMutation({
    mutationFn: () => api.booking.cancel(bookingId),
    onSuccess: () => {},
  });
  const pay = useMutation({
    mutationFn: () => api.payments.init({ bookingId, method: "mock", returnUrl: window.location.origin }),
    onSuccess: () => {},
  });

  if (isFetching && !b) return <div className="p-4"><Busy /></div>;
  if (!b)
    return (
      <div className="p-4">
        <Callout tone="error">Không tìm thấy đơn.</Callout>
      </div>
    );

  const showQr = b.status === "Confirmed" || b.status === "Completed";
  const canPay = b.status === "Pending" && !staff;
  const p = b.price;

  return (
    <div className="mx-auto max-w-2xl p-4 md:p-6">
      <Link to="/bookings" className="mb-4 inline-flex items-center gap-1 text-sm text-muted hover:text-fg">
        <ArrowLeft className="h-4 w-4" /> Đơn của tôi
      </Link>

      <div className="flex items-start justify-between">
        <div>
          <p className="text-sm text-muted">Mã đặt</p>
          <h1 className="text-xl font-bold tnum">{b.bookingCode}</h1>
        </div>
        <StatusBadge status={b.status} />
      </div>

      <Card className="mt-4">
        <div className="flex items-center justify-between gap-4">
          <div>
            <p className="font-semibold">{b.courtName}</p>
            <p className="text-sm text-muted tnum">
              {formatDay(b.date)} • {b.slotLabel} • {b.playerCount} người
            </p>
          </div>
          {showQr && <QrTile code={b.bookingCode} />}
        </div>

        {showQr && (
          <p className="mt-3 flex items-center gap-1 text-xs text-muted">
            <QrCode className="h-3.5 w-3.5" /> Xuất trình mã này để vào sân.
          </p>
        )}
      </Card>

      {/* Price breakdown (docs 07 §4: show every line clearly) */}
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

      {/* Pay CTA */}
      {canPay && (
        <Card className="mt-4">
          <p className="mb-3 flex items-center gap-1 text-sm text-muted">
            <CreditCard className="h-4 w-4" />{" "}
            {pays?.some((x) => x.status === "Paid")
              ? "Đã có giao dịch thanh toán, vui lòng đối chiếu."
              : "Đang giữ sân 10 phút. Vui lòng hoàn tất thanh toán."}
          </p>
          <Button variant="accent" className="w-full" onClick={() => pay.mutate()} disabled={pay.isPending}>
            {pay.isPending ? "Đang tạo thanh toán…" : "Thanh toán"}
          </Button>
          {pay.isError && (
            <Callout tone="error" className="mt-2">
              Thanh toán chưa hoàn tất. Bạn có thể thử lại hoặc hủy để giải phóng slot.
            </Callout>
          )}
        </Card>
      )}

      {/* Cancel (docs 07 §5) */}
      {b.status === "Pending" && (
        <div className="mt-4">
          <Button variant="danger" className="w-full" onClick={() => cancel.mutate()} disabled={cancel.isPending}>
            Huỷ đặt chỗ
          </Button>
          {cancel.isError && (
            <Callout tone="error" className="mt-2">
              {cancel.error instanceof Error ? cancel.error.message : "Không hủy được."}
            </Callout>
          )}
        </div>
      )}
    </div>
  );
}
