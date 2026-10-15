import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { PageHeader, Busy, Button } from "../../components/ui";
import { api, type AuditEntry, type Paged } from "../../lib/api";
import { VirtualizedTable, type Column } from "../../components/VirtualizedTable";
import { clsx } from "clsx";

const ENTITY_FILTERS = [
  { key: "", label: "Tất cả" },
  { key: "Booking", label: "Đặt chỗ" },
  { key: "Branch", label: "Cơ sở" },
  { key: "Court", label: "Sân" },
  { key: "Voucher", label: "Voucher" },
  { key: "User", label: "Tài khoản" },
];

function tsToVi(iso: string): string {
  if (!iso) return "—";
  const d = new Date(iso);
  return `${d.toLocaleDateString("vi-VN")} ${d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`;
}

/**
 * Admin "Nhật ký" (docs 07 §4): audit log paged + filter theo entity.
 * Virtualized để mượt khi có nhiều entry (500 user seeded → hàng trăm log).
 */
export default function AuditPage() {
  const [entity, setEntity] = useState("");
  const [page, setPage] = useState(1);

  const { data, isFetching } = useQuery({
    queryKey: ["audit", entity, page],
    queryFn: () =>
      api.admin.audit.query({
        entityType: entity || undefined,
        page,
        pageSize: 100,
      }) as Promise<Paged<AuditEntry>>,
    placeholderData: (prev) => prev,
  });

  const columns: Column<AuditEntry>[] = [
    { key: "time", label: "Thời gian", width: 180, sortable: true, render: (a) => <span className="tnum text-muted">{tsToVi(a.createdAt)}</span> },
    { key: "actor", label: "Người thao tác", width: 180, render: (a) => a.actorName ?? (a.actorId ? `User #${a.actorId}` : "system") },
    {
      key: "action", label: "Thao tác", width: 160, sortable: true,
      render: (a) => (
        <span className="rounded bg-bg px-1.5 py-0.5 font-mono text-xs text-muted">{a.action}</span>
      ),
    },
    { key: "entity", label: "Đối tượng", width: 160, render: (a) => `${a.entityType}${a.entityId ? ` #${a.entityId}` : ""}` },
    { key: "reason", label: "Lý do", width: 200, render: (a) => <span className="text-xs text-muted">{a.reason ?? "—"}</span> },
  ];

  return (
    <div>
      <PageHeader title="Nhật ký" subtitle="Toàn bộ thao tác quản trị (audit log)" />

      <div className="mt-4 flex flex-wrap gap-2" role="tablist" aria-label="Lọc theo đối tượng">
        {ENTITY_FILTERS.map((f) => (
          <button
            key={f.key}
            role="tab"
            aria-selected={entity === f.key}
            onClick={() => { setEntity(f.key); setPage(1); }}
            className={clsx(
              "rounded-full px-3 py-1.5 text-xs font-medium transition-colors cursor-pointer",
              entity === f.key ? "bg-primary text-white" : "border border-border text-muted hover:text-fg",
            )}
          >
            {f.label}
          </button>
        ))}
      </div>

      <div className="mt-4">
        {isFetching && !data ? (
          <Busy label="Đang tải nhật ký…" />
        ) : (
          <>
            <VirtualizedTable
              rows={data?.items ?? []}
              columns={columns}
              rowKey={(a) => a.id}
              maxHeight={520}
              emptyLabel="Không có nhật ký nào."
            />
            <div className="mt-3 flex items-center justify-between text-sm text-muted">
              <span>{data?.totalItems ?? 0} bản ghi · Trang {data?.currentPage ?? 1}</span>
              <div className="flex gap-2">
                <Button variant="ghost" className="h-9 min-h-0 px-3 text-xs" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Trước</Button>
                <Button variant="ghost" className="h-9 min-h-0 px-3 text-xs" disabled={page >= (data?.totalPages ?? 1)} onClick={() => setPage((p) => p + 1)}>Sau</Button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
