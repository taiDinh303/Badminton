import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { PageHeader, StatusBadge, Busy, Callout, Button } from "../../components/ui";
import { VirtualizedTable, type Column } from "../../components/VirtualizedTable";
import { api, formatDay, formatVnd, type Booking } from "../../lib/api";
import { clsx } from "clsx";

const STATUS_TABS: { key: string; label: string }[] = [
  { key: "", label: "Tất cả" },
  { key: "Pending", label: "Chờ thanh toán" },
  { key: "Confirmed", label: "Đã xác nhận" },
  { key: "Completed", label: "Đã hoàn thành" },
  { key: "Cancelled", label: "Đã hủy" },
  { key: "NoShow", label: "No-show" },
  { key: "Refunded", label: "Đã hoàn tiền" },
];

/**
 * Admin "Đặt chỗ" (docs 07 §4): paged, virtualized, filter theo trạng thái,
 * với các thao tác Manager/Admin (hoàn thành / no-show / hoàn tiền).
 * pageSize=100 nhưng VirtualizedTable chỉ render ~30 dòng → mượt dù nhiều đơn.
 */
export default function BookingsPage() {
  const [status, setStatus] = useState("");
  const [page, setPage] = useState(1);

  const { data, isFetching, refetch } = useQuery({
    queryKey: ["admin-bookings", status, page],
    queryFn: () => api.booking.all(status || undefined, undefined, page, 100),
    placeholderData: (prev) => prev,
  });

  const complete = useMutation({
    mutationFn: (id: number) => api.booking.complete(id, "Quản trị viên xác nhận"),
    onSuccess: () => refetch(),
  });
  const noShow = useMutation({
    mutationFn: (id: number) => api.booking.noShow(id, "Khách không đến"),
    onSuccess: () => refetch(),
  });
  const refund = useMutation({
    mutationFn: (id: number) => api.payments.refund(id, "Quản trị hoàn tiền"),
    onSuccess: () => refetch(),
  });

  const err =
    (complete.error as Error | undefined)?.message ??
    (noShow.error as Error | undefined)?.message ??
    (refund.error as Error | undefined)?.message ??
    null;

  const columns: Column<Booking>[] = [
    { key: "bookingCode", label: "Mã", width: 120, sortable: true, render: (b) => <span className="tnum font-medium">{b.bookingCode}</span> },
    { key: "date", label: "Ngày · Giờ", width: 160, sortable: true, render: (b) => <span className="tnum text-muted">{formatDay(b.date)} · {b.slotLabel}</span> },
    { key: "courtName", label: "Sân", width: 150, sortable: true, render: (b) => b.courtName },
    { key: "userName", label: "Khách", width: 150, render: (b) => b.userName ?? "-" },
    { key: "payableAmount", label: "Thanh toán", width: 110, align: "right", sortable: true, render: (b) => <span className="tnum font-medium">{formatVnd(b.payableAmount)}</span> },
    { key: "status", label: "Trạng thái", width: 150, render: (b) => <StatusBadge status={b.status} /> },
    {
      key: "actions",
      label: "Thao tác",
      width: 240,
      align: "right",
      render: (b) => (
        <div className="flex gap-1">
          {b.status === "Confirmed" && (
            <>
              <Button variant="ghost" className="h-8 min-h-0 px-2 text-xs" onClick={() => complete.mutate(b.id)} disabled={complete.isPending}>Hoàn thành</Button>
              <Button variant="ghost" className="h-8 min-h-0 px-2 text-xs" onClick={() => noShow.mutate(b.id)} disabled={noShow.isPending}>No-show</Button>
              <Button variant="ghost" className="h-8 min-h-0 px-2 text-xs" onClick={() => refund.mutate(b.id)} disabled={refund.isPending}>Hoàn tiền</Button>
            </>
          )}
          {b.status !== "Confirmed" && <span className="text-xs text-muted">—</span>}
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title="Đặt chỗ" subtitle="Tất cả đơn của mọi khách (quản trị)" />

      <div className="mt-4 flex flex-wrap gap-2" role="tablist" aria-label="Lọc theo trạng thái">
        {STATUS_TABS.map((t) => (
          <button
            key={t.key}
            role="tab"
            aria-selected={status === t.key}
            onClick={() => { setStatus(t.key); setPage(1); }}
            className={clsx(
              "rounded-full px-3 py-1.5 text-xs font-medium transition-colors cursor-pointer",
              status === t.key ? "bg-primary text-white" : "border border-border text-muted hover:text-fg",
            )}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div className="mt-4">
        {isFetching && !data ? (
          <Busy label="Đang tải đơn…" />
        ) : (
          <>
            <VirtualizedTable
              rows={data?.items ?? []}
              columns={columns}
              rowKey={(b) => b.id}
              maxHeight={520}
              emptyLabel="Chưa có đơn nào với bộ lọc hiện tại."
            />
            <div className="mt-3 flex items-center justify-between text-sm text-muted">
              <span>
                {data?.totalItems ?? 0} đơn · Trang {data?.currentPage ?? 1}
              </span>
              <div className="flex gap-2">
                <Button variant="ghost" className="h-9 min-h-0 px-3 text-xs" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Trước</Button>
                <Button variant="ghost" className="h-9 min-h-0 px-3 text-xs" disabled={page >= (data?.totalPages ?? 1)} onClick={() => setPage((p) => p + 1)}>Sau</Button>
              </div>
            </div>
          </>
        )}
      </div>

      {err && <Callout tone="error" className="mt-3">{err}</Callout>}
    </div>
  );
}
