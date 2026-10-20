import { useMemo, useState, type ReactNode } from "react";
import { useMutation, useQuery, type UseMutationResult } from "@tanstack/react-query";
import {
  CalendarDays,
  Dumbbell,
  User,
  RefreshCw,
  ChevronLeft,
  ChevronRight,
} from "lucide-react";
import {
  StatusBadge,
  Busy,
  Callout,
  Button,
  Toolbar,
  Pills,
  Card,
} from "../../../components/ui";
import { api, type Booking } from "../../../services/api";
import { formatDay, formatVnd, slotLabelClean } from "../../../utils/format";

const PAGE_SIZE = 10;

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
 * Admin "Đặt chỗ" (docs 07 §4) — layout card (mỗi đơn 1 thẻ):
 *   - Toolbar: tìm kiếm + lọc cơ sở + làm mới
 *   - Pills: lọc theo trạng thái
 *   - Card: badge trạng thái + mã đơn, 3 dòng (ngày, sân, khách),
 *     thanh giá + nút thao tác theo trạng thái
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
        PAGE_SIZE,
      ),
    placeholderData: (prev) => prev,
  });

  const onSite = useMutation({
    mutationFn: (id: number) => api.payments.onSite(id),
    onSuccess: () => refetch(),
  });
  const complete = useMutation({
    mutationFn: (id: number) =>
      api.booking.complete(id, "Quản trị viên xác nhận"),
    onSuccess: () => refetch(),
  });
  const noShow = useMutation({
    mutationFn: (id: number) => api.booking.noShow(id, "Khách không đến"),
    onSuccess: () => refetch(),
  });
  const refund = useMutation({
    mutationFn: (id: number) =>
      api.payments.refund(id, "Quản trị hoàn tiền"),
    onSuccess: () => refetch(),
  });

  const err =
    (onSite.error as Error | undefined)?.message ??
    (complete.error as Error | undefined)?.message ??
    (noShow.error as Error | undefined)?.message ??
    (refund.error as Error | undefined)?.message ??
    null;

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

  const totalPages = data?.totalPages ?? 1;

  return (
    <div>
      <Toolbar
        title="Đặt chỗ"
        subtitle="Tất cả đơn của mọi khách (quản trị)"
        search={{
          value: q,
          onChange: setQ,
          placeholder: "Tìm mã đơn, khách, sân…",
        }}
        actions={
          <>
            <select
              value={branchId}
              onChange={(e) => {
                setBranchId(e.target.value === "" ? "" : Number(e.target.value));
                setPage(1);
              }}
              aria-label="Lọc theo cơ sở"
              className="h-9 rounded-lg border border-border bg-card px-3 text-sm text-fg outline-none focus:border-primary"
            >
              <option value="">Tất cả cơ sở</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
            <Button variant="ghost" size="sm" onClick={() => refetch()}>
              <RefreshCw className="h-4 w-4" /> Làm mới
            </Button>
          </>
        }
      />

      <div className="mt-4">
        <Pills
          options={STATUS_TABS.map((t) => ({ key: t.key, label: t.label }))}
          value={status as (typeof STATUS_TABS)[number]["key"]}
          onChange={(v) => {
            setStatus(v);
            setPage(1);
          }}
          ariaLabel="Lọc theo trạng thái"
        />
      </div>

      <div className="mt-4">
        {isFetching && !data ? (
          <Busy label="Đang tải đơn…" />
        ) : items.length === 0 ? (
          <Callout tone="info">Không có đơn nào khớp bộ lọc.</Callout>
        ) : (
          <div className="grid gap-3 lg:grid-cols-2 xl:grid-cols-3">
            {items.map((b) => (
              <BookingCard
                key={b.id}
                b={b}
                onSite={onSite}
                complete={complete}
                noShow={noShow}
                refund={refund}
              />
            ))}
          </div>
        )}

        {/* Pagination */}
        <div className="mt-4 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
          <span className="tnum">
            {items.length} đơn · Trang {data?.currentPage ?? page}/{totalPages}
          </span>
          <div className="flex items-center gap-1">
            <PageBtn
              disabled={page <= 1 || !data?.hasPreviousPage}
              onClick={() => setPage(1)}
              ariaLabel="Trang đầu"
            >
              «
            </PageBtn>
            <PageBtn
              disabled={page <= 1 || !data?.hasPreviousPage}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              ariaLabel="Trang trước"
            >
              <ChevronLeft className="h-4 w-4" />
            </PageBtn>
            {pageNumbers(page, totalPages).map((n, i) =>
              n === "…" ? (
                <span key={`gap-${i}`} className="px-1.5 text-muted">
                  …
                </span>
              ) : (
                <button
                  key={n}
                  type="button"
                  onClick={() => setPage(n)}
                  aria-current={page === n ? "page" : undefined}
                  className={
                    "flex h-8 min-w-8 items-center justify-center rounded-lg px-1.5 text-sm font-semibold transition-colors tnum " +
                    (page === n
                      ? "bg-primary text-white"
                      : "border border-border bg-card text-muted hover:text-fg")
                  }
                >
                  {n}
                </button>
              ),
            )}
            <PageBtn
              disabled={page >= totalPages || !data?.hasNextPage}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              ariaLabel="Trang sau"
            >
              <ChevronRight className="h-4 w-4" />
            </PageBtn>
            <PageBtn
              disabled={page >= totalPages || !data?.hasNextPage}
              onClick={() => setPage(totalPages)}
              ariaLabel="Trang cuối"
            >
              »
            </PageBtn>
          </div>
        </div>
      </div>

      {err && <Callout tone="error" className="mt-3">{err}</Callout>}
    </div>
  );
}

/** Số trang cần hiển thị: đầu 1–2, … quanh trang hiện, … cuối. */
function pageNumbers(page: number, totalPages: number): (number | "…")[] {
  if (totalPages <= 7)
    return Array.from({ length: totalPages }, (_, i) => i + 1);
  const pages: (number | "…")[] = [1];
  const lo = Math.max(2, page - 1);
  const hi = Math.min(totalPages - 1, page + 1);
  if (lo > 2) pages.push("…");
  for (let i = lo; i <= hi; i++) pages.push(i);
  if (hi < totalPages - 1) pages.push("…");
  pages.push(totalPages);
  return pages;
}

function PageBtn({
  children,
  disabled,
  onClick,
  ariaLabel,
}: {
  children: ReactNode;
  disabled: boolean;
  onClick: () => void;
  ariaLabel: string;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      aria-label={ariaLabel}
      className="flex h-8 min-w-8 items-center justify-center rounded-lg border border-border bg-card px-1.5 text-sm text-muted transition-colors hover:text-fg disabled:opacity-40"
    >
      {children}
    </button>
  );
}

// ── Booking card ──────────────────────────────────────────────────────
function BookingCard({
  b,
  onSite,
  complete,
  noShow,
  refund,
}: {
  b: Booking;
  onSite: UseMutationResult<unknown, Error, number, unknown>;
  complete: UseMutationResult<Booking, Error, number, unknown>;
  noShow: UseMutationResult<Booking, Error, number, unknown>;
  refund: UseMutationResult<unknown, Error, number, unknown>;
}) {
  const busy =
    onSite.isPending ||
    complete.isPending ||
    noShow.isPending ||
    refund.isPending;

  return (
    <Card className="p-4">
      {/* Header: status + code */}
      <div className="flex items-center justify-between gap-2">
        <StatusBadge status={b.status} />
        <span className="font-mono text-xs font-semibold text-muted tnum">
          {b.bookingCode}
        </span>
      </div>

      {/* Meta: date · court · customer */}
      <dl className="mt-3 space-y-2 text-sm">
        <div className="flex items-center gap-2 text-muted">
          <CalendarDays className="h-4 w-4 shrink-0" />
          <span className="tnum">
            {formatDay(b.date)} · {slotLabelClean(b.slotLabel)}
          </span>
        </div>
        <div className="flex items-center gap-2 text-muted">
          <Dumbbell className="h-4 w-4 shrink-0" />
          <span className="text-fg">{b.courtName}</span>
        </div>
        <div className="flex items-center gap-2 text-muted">
          <User className="h-4 w-4 shrink-0" />
          <span className="text-fg">
            {b.userName ?? <span className="text-muted">—</span>}
          </span>
        </div>
      </dl>

      {/* Price */}
      <div className="mt-3 flex items-center justify-between border-t border-border pt-3">
        <span className="text-xs text-muted">Thanh toán</span>
        <span className="text-base font-bold tnum">
          {formatVnd(b.payableAmount)}
        </span>
      </div>

      {/* Actions */}
      {b.status === "Pending" ? (
        <Button
          variant="primary"
          size="sm"
          className="mt-3 w-full"
          onClick={() => onSite.mutate(b.id)}
          disabled={busy}
        >
          {onSite.isPending ? "Đang xác nhận…" : "Xác nhận tại quầy"}
        </Button>
      ) : b.status === "Confirmed" ? (
        <div className="mt-3 grid grid-cols-3 gap-1.5">
          <Button
            variant="ghost"
            size="xs"
            onClick={() => complete.mutate(b.id)}
            disabled={busy}
          >
            Hoàn thành
          </Button>
          <Button
            variant="ghost"
            size="xs"
            onClick={() => noShow.mutate(b.id)}
            disabled={busy}
          >
            No-show
          </Button>
          <Button
            variant="danger"
            size="xs"
            onClick={() => refund.mutate(b.id)}
            disabled={busy}
          >
            Hoàn tiền
          </Button>
        </div>
      ) : null}
    </Card>
  );
}
