import { useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { ScrollText } from "lucide-react";
import {
  Toolbar,
  Busy,
  Button,
  Pills,
  Pagination,
  Callout,
} from "../../../components/ui";
import { api, type AuditEntry, type Paged } from "../../../services/api";
import { formatDay } from "../../../utils/format";
import { VirtualizedTable, type Column } from "../../../components/VirtualizedTable";

const ENTITY_FILTERS = [
  { key: "", label: "Tất cả" },
  { key: "Booking", label: "Đặt chỗ" },
  { key: "Branch", label: "Cơ sở" },
  { key: "Court", label: "Sân" },
  { key: "CourtDateStatus", label: "Bảo trì sân" },
  { key: "Voucher", label: "Voucher" },
  { key: "User", label: "Tài khoản" },
  { key: "TimeSlot", label: "Khung giờ" },
  { key: "Payment", label: "Thanh toán" },
  { key: "Review", label: "Đánh giá" },
];

/** Human label for a backend entity type (docs 03: mọi entity được audit). */
const ENTITY_LABEL: Record<string, string> = {
  Booking: "Đơn đặt",
  Branch: "Cơ sở",
  Court: "Sân",
  CourtDateStatus: "Bảo trì sân",
  Voucher: "Voucher",
  User: "Tài khoản",
  Staff: "Nhân viên",
  TimeSlot: "Khung giờ",
  Payment: "Thanh toán",
  Review: "Đánh giá",
};

/**
 * Decode an audit entityId. Most are plain ids, but the maintenance service
 * logs a composite "courtId@yyyy-MM-dd" (docs 03 §8) → render it readably.
 */
function renderEntityId(entityType: string, entityId: string | null): ReactNode {
  if (!entityId) return null;
  if (entityType === "CourtDateStatus" && entityId.includes("@")) {
    const [courtId, day] = entityId.split("@");
    return (
      <>
        Sân #{courtId} · <span className="tnum">{formatDay(day)}</span>
      </>
    );
  }
  if (/^\d+$/.test(entityId)) return <span className="tnum">#{entityId}</span>;
  return entityId;
}

function tsToVi(iso: string): string {
  if (!iso) return "—";
  const d = new Date(iso);
  return `${d.toLocaleDateString("vi-VN")} ${d.toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}`;
}

/**
 * Admin "Nhật ký" (docs 07 §4): audit log paged + filter theo entity.
 */
export default function AuditPage() {
  const [entity, setEntity] = useState("");
  const [page, setPage] = useState(1);

  const { data, isFetching, isError } = useQuery({
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
    {
      key: "actor",
      label: "Người thao tác",
      width: 180,
      render: (a) => (
        <span className="inline-flex items-center gap-1.5 text-sm font-medium">
          <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary/10 text-[10px] font-bold text-primary">
            {(a.actorName ?? "S").charAt(0).toUpperCase()}
          </span>
          {a.actorName ?? (a.actorId ? `User #${a.actorId}` : "system")}
        </span>
      ),
    },
    {
      key: "action",
      label: "Thao tác",
      width: 160,
      sortable: true,
      render: (a) => (
        <span className="rounded bg-bg px-1.5 py-0.5 font-mono text-xs text-muted">{a.action}</span>
      ),
    },
    {
      key: "entity",
      label: "Đối tượng",
      width: 200,
      render: (a) => (
        <span className="inline-flex items-center gap-1.5 text-sm">
          <ScrollText className="h-3.5 w-3.5 shrink-0 text-muted" />
          <span className="font-medium">{ENTITY_LABEL[a.entityType] ?? a.entityType}</span>
          {renderEntityId(a.entityType, a.entityId)}
        </span>
      ),
    },
    { key: "reason", label: "Lý do", width: 200, render: (a) => <span className="text-xs text-muted">{a.reason ?? "—"}</span> },
  ];

  return (
    <div>
      <Toolbar
        title="Nhật ký"
        subtitle="Toàn bộ thao tác quản trị (audit log)"
        actions={
          <Button variant="ghost" size="sm" onClick={() => setPage(1)}>
            Làm mới
          </Button>
        }
      />

      <div className="mt-4">
        <Pills
          options={ENTITY_FILTERS.map((f) => ({ key: f.key, label: f.label }))}
          value={entity as (typeof ENTITY_FILTERS)[number]["key"]}
          onChange={(v) => { setEntity(v); setPage(1); }}
          ariaLabel="Lọc theo đối tượng"
        />
      </div>

      <div className="mt-4">
        {isError && <Callout tone="error">Không tải được nhật ký.</Callout>}
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
            <div className="mt-3">
              <Pagination
                page={data?.currentPage ?? page}
                totalPages={data?.totalPages ?? 1}
                totalItems={data?.totalItems}
                onPrev={() => setPage((p) => Math.max(1, p - 1))}
                onNext={() => setPage((p) => p + 1)}
              />
            </div>
          </>
        )}
      </div>
    </div>
  );
}
