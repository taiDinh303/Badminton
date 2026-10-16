import { useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import {
  StatusBadge,
  Busy,
  Callout,
  Button,
  Toolbar,
  Pills,
} from "../../components/ui";
import { VirtualizedTable, type Column } from "../../components/VirtualizedTable";
import { api, formatDay, formatVnd, type Booking } from "../../lib/api";

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
 * Admin "Đặt chỗ" (docs 07 §4): paged, virtualized, filter theo trạng thái
 * + cơ sở + tìm kiếm (mã đơn / khách), thao tác Manager/Admin.
 */
export default function BookingsPage() {
  const [status, setStatus] = useState("");
  const [branchId, setBranchId] = useState<number | "">("");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);

  const { data: branches = [] } = useQuery({
    queryKey: ["branches"],
    queryFn: api.branches.list,
  });

  const { data, isFetching, refetch } = useQuery({
    queryKey: ["admin-bookings", status, branchId, page],
    queryFn: () =>
      api.booking.all(
        status || undefined,
        branchId === "" ? undefined : branchId,
        page,
        100,
      ),
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

  // Client-side filter over the current page (search mã đơn / khách / sân).
  const items = useMemo(() => {
    const list = data?.items ?? [];
    if (!q.trim()) return list;
    const t = q.trim().toLowerCase();
    return list.filter((b) =>
      [b.bookingCode, b.userName, b.courtName, b.slotLabel]
        .filter(Boolean)
        .some((v) => String(v).toLowerCase().includes(t)),
    );
  }, [data, q]);

  const columns: Column<Booking>[] = [
    {
      key: "bookingCode",
      label: "Mã đơn",
      width: 130,
      sortable: true,
      render: (b) => <span className="font-mono text-xs font-semibold tnum">{b.bookingCode}</span>,
    },
    {
      key: "date",
      label: "Ngày · Giờ",
      width: 160,
      sortable: true,
      render: (b) => <span className="text-muted tnum">{formatDay(b.date)} · {b.slotLabel}</span>,
    },
    { key: "courtName", label: "Sân", width: 140, sortable: true, render: (b) => b.courtName },
    {
      key: "userName",
      label: "Khách",
      width: 150,
      render: (b) => b.userName ?? <span className="text-muted">—</span>,
    },
    {
      key: "payableAmount",
      label: "Thanh toán",
      width: 120,
      align: "right",
      sortable: true,
      render: (b) => <span className="tnum font-semibold">{formatVnd(b.payableAmount)}</span>,
    },
    { key: "status", label: "Trạng thái", width: 150, render: (b) => <StatusBadge status={b.status} size="sm" /> },
    {
      key: "actions",
      label: "Thao tác",
      width: 260,
      align: "right",
      render: (b) => (
        <div className="flex items-center justify-end gap-1.5">
          {b.status === "Confirmed" ? (
            <>
              <Button variant="ghost" size="xs" onClick={() => complete.mutate(b.id)} disabled={complete.isPending}>
                Hoàn thành
              </Button>
              <Button variant="ghost" size="xs" onClick={() => noShow.mutate(b.id)} disabled={noShow.isPending}>
                No-show
              </Button>
              <Button variant="danger" size="xs" onClick={() => refund.mutate(b.id)} disabled={refund.isPending}>
                Hoàn tiền
              </Button>
            </>
          ) : (
            <span className="text-xs text-muted">—</span>
          )}
        </div>
      ),
    },
  ];

  return (
    <div>
      <Toolbar
        title="Đặt chỗ"
        subtitle="Tất cả đơn của mọi khách (quản trị)"
        search={{ value: q, onChange: (v) => { setQ(v); }, placeholder: "Tìm mã đơn, khách, sân…" }}
        actions={
          <>
            <select
              value={branchId}
              onChange={(e) => { setBranchId(e.target.value === "" ? "" : Number(e.target.value)); setPage(1); }}
              aria-label="Lọc theo cơ sở"
              className="h-9 rounded-lg border border-border bg-card px-3 text-sm text-fg outline-none focus:border-primary"
            >
              <option value="">Tất cả cơ sở</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </select>
            <Button variant="ghost" size="sm" onClick={() => refetch()}>
              Làm mới
            </Button>
          </>
        }
      />

      <div className="mt-4">
        <Pills
          options={STATUS_TABS.map((t) => ({ key: t.key, label: t.label }))}
          value={status as (typeof STATUS_TABS)[number]["key"]}
          onChange={(v) => { setStatus(v); setPage(1); }}
          ariaLabel="Lọc theo trạng thái"
        />
      </div>

      <div className="mt-4">
        {isFetching && !data ? (
          <Busy label="Đang tải đơn…" />
        ) : (
          <>
            <VirtualizedTable
              rows={items}
              columns={columns}
              rowKey={(b) => b.id}
              maxHeight={520}
              emptyLabel="Không có đơn nào khớp bộ lọc."
            />
            <div className="mt-3 flex items-center justify-between text-sm text-muted">
              <span className="tnum">
                {items.length} đơn · Trang {data?.currentPage ?? page}/{data?.totalPages ?? 1}
              </span>
              <div className="flex gap-2">
                <Button variant="ghost" size="sm" disabled={page <= 1 || !data?.hasPreviousPage} onClick={() => setPage((p) => p - 1)}>
                  Trước
                </Button>
                <Button variant="ghost" size="sm" disabled={page >= (data?.totalPages ?? 1) || !data?.hasNextPage} onClick={() => setPage((p) => p + 1)}>
                  Sau
                </Button>
              </div>
            </div>
          </>
        )}
      </div>

      {err && <Callout tone="error" className="mt-3">{err}</Callout>}
    </div>
  );
}
