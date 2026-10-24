import { useBookingRefresh } from "../hooks/useBookingRefresh";
import { useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Ticket } from "lucide-react";
import { api } from "../../../services/api";
import { formatDay, formatVnd } from "../../../utils/format";
import { Busy, Button, Callout, Card, StatusBadge } from "../../../components/ui";
import { isStaff } from "../../../store/auth";
import { useAuth } from "../../../store/auth";
import { clsx } from "clsx";

const TABS: { key: string; label: string }[] = [
  { key: "", label: "Tất cả" },
  { key: "Pending", label: "Chờ thanh toán" },
  { key: "Confirmed", label: "Đã xác nhận" },
  { key: "Completed", label: "Đã hoàn thành" },
  { key: "Cancelled", label: "Đã hủy" },
  { key: "NoShow", label: "No-show" },
];

/**
 * Đơn của tôi (docs 07 §4): danh sách sắp diễn ra/đã qua/đã hủy, điểm, voucher;
 * các trạng thái rỗng/chờ/hoàn thành/hủy có hoàn tiền; filter trạng thái.
 */
export default function MyBookingsPage() {
  const refreshBookings = useBookingRefresh();
  const user = useAuth((s) => s.user);
  const [tab, setTab] = useState("");
  const [page, setPage] = useState(1);
  const staff = isStaff(user);

  const { data, isLoading, error: loadError } = useQuery({
    queryKey: ["my-bookings", tab, page],
    queryFn: () => api.booking.mine(tab || undefined, page),
    placeholderData: (prev) => prev,
  });

  const cancel = useMutation({
    mutationFn: (id: number) => api.booking.cancel(id),
    onSuccess: refreshBookings,
  });
  const complete = useMutation({
    mutationFn: (id: number) => api.booking.complete(id),
    onSuccess: refreshBookings,
  });
  const noShow = useMutation({
    mutationFn: (id: number) => api.booking.noShow(id),
    onSuccess: refreshBookings,
  });
  const refund = useMutation({
    mutationFn: (id: number) => api.payments.refund(id, "Khách yêu cầu hoàn"),
    onSuccess: refreshBookings,
  });

  const err =
    loadError?.message ??
    (cancel.error as Error | undefined)?.message ??
    (complete.error as Error | undefined)?.message ??
    (noShow.error as Error | undefined)?.message ??
    (refund.error as Error | undefined)?.message ??
    null;

  if (isLoading && !data) return <div className="p-4"><Busy /></div>;

  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <h1 className="text-2xl font-bold">Đơn của tôi</h1>

      <div className="mt-4 flex flex-wrap gap-2" role="tablist" aria-label="Lọc đơn">
        {TABS.map((t) => (
          <button
            key={t.key}
            role="tab"
            aria-selected={tab === t.key}
            onClick={() => { setTab(t.key); setPage(1); }}
            className={clsx(
              "rounded-full px-3 py-1.5 text-xs font-medium transition-colors",
              tab === t.key
                ? "bg-primary text-white"
                : "border border-border text-muted hover:text-fg",
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div className="mt-4 space-y-3">
        {data?.items.length === 0 && (
          <Callout tone="info">Chưa có đơn đặt sân nào.</Callout>
        )}

        {data?.items.map((b) => (
          <Card key={b.id} className="p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div className="min-w-0">
                <Link
                  to={`/bookings/${b.id}`}
                  className="flex items-center gap-1 text-sm font-semibold text-primary hover:underline"
                >
                  <Ticket className="h-4 w-4" />
                  {b.bookingCode}
                </Link>
                <p className="mt-0.5 text-sm text-muted tnum">
                  {b.courtName} • {formatDay(b.date)} • {b.slotLabel}
                </p>
                <p className="text-sm font-medium tnum">
                  {formatVnd(b.payableAmount)}
                  {b.pointsRedeemed > 0 && (
                    <span className="ml-2 text-xs text-muted">
                      (đã dùng {b.pointsRedeemed.toLocaleString("vi-VN")} điểm)
                    </span>
                  )}
                </p>
              </div>
              <StatusBadge status={b.status} />
            </div>

            <div className="mt-3 flex flex-wrap gap-2">
              {["Pending", "Confirmed"].includes(b.status) && (
                <Button variant="danger" className="text-xs" onClick={() => cancel.mutate(b.id)} disabled={cancel.isPending}>
                  Huỷ đơn
                </Button>
              )}
              {staff && b.status === "Confirmed" && (
                <>
                  <Button variant="ghost" className="text-xs" onClick={() => complete.mutate(b.id)} disabled={complete.isPending}>Hoàn thành</Button>
                  <Button variant="ghost" className="text-xs" onClick={() => noShow.mutate(b.id)} disabled={noShow.isPending}>No-show</Button>
                  <Button variant="ghost" className="text-xs" onClick={() => refund.mutate(b.id)} disabled={refund.isPending}>Hoàn tiền</Button>
                </>
              )}
              <Link to={`/bookings/${b.id}`}>
                <Button variant="ghost" className="text-xs">Chi tiết</Button>
              </Link>
            </div>

            {err && <Callout tone="error" className="mt-2">{err}</Callout>}
          </Card>
        ))}
      </div>

      {data && data.totalPages > 1 && (
        <div className="mt-4 flex items-center justify-center gap-3 text-sm text-muted">
          <Button variant="ghost" disabled={!data.hasPreviousPage} onClick={() => setPage((p) => p - 1)}>Trước</Button>
          <span className="tnum">Trang {data.currentPage}/{data.totalPages}</span>
          <Button variant="ghost" disabled={!data.hasNextPage} onClick={() => setPage((p) => p + 1)}>Sau</Button>
        </div>
      )}
    </div>
  );
}
