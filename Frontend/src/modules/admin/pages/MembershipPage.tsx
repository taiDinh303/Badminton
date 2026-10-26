import { IssueRecoveryCode } from "../../auth/components/PasswordRecovery";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Crown, Medal, RefreshCw } from "lucide-react";
import {
  Busy,
  Callout,
  Button,
  Toolbar,
  Card,
} from "../../../components/ui";
import { VirtualizedTable, type Column } from "../../../components/VirtualizedTable";
import { api, type MembershipView, type MembershipTier } from "../../../services/api";
import { formatDay } from "../../../utils/format";

const TIER_META: Record<string, { label: string; mult: string; tone: string }> = {
  Gold: { label: "Gold", mult: "2.0x", tone: "text-accent" },
  Silver: { label: "Silver", mult: "1.5x", tone: "text-muted" },
  None: { label: "Khách thường", mult: "1.0x", tone: "text-muted" },
};

/**
 * FEAT 4 — Membership V2 (docs 07 §4). List paid members + assign a tier to
 * a customer. Admin only (backend enforces [Authorize(Roles="Admin")]).
 */
export default function MembershipPage() {
  const qc = useQueryClient();
  const [recovery, setRecovery] = useState<MembershipView | null>(null);
  const [search, setSearch] = useState("");
  const customersQuery = useQuery({ queryKey: ["membership", "customers"], queryFn: api.admin.membership.customers });
  const customers = customersQuery.data ?? [];
  const [assignOpen, setAssignOpen] = useState(false);
  const [targetUser, setTargetUser] = useState("");
  const [tier, setTier] = useState<MembershipTier>("Silver");

  const { data: members = [], isLoading, refetch, error: loadError } = useQuery({
    queryKey: ["membership", "members"],
    queryFn: api.admin.membership.members,
  });

  const assign = useMutation({
    mutationFn: () => api.admin.membership.assign(Number(targetUser), tier),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["membership"] });
      qc.invalidateQueries({ queryKey: ["account", "profile"] });
      setAssignOpen(false);
      setTargetUser("");
    },
  });

  const err = (assign.error as Error | undefined)?.message ?? null;

  const columns: Column<MembershipView>[] = [
    { key: "fullName", label: "Khách", width: 180, sortable: true, render: (m) => <span className="font-medium">{m.fullName}</span> },
    { key: "phoneNumber", label: "SĐT", width: 140, render: (m) => <span className="tnum">{m.phoneNumber}</span> },
    {
      key: "tier",
      label: "Hạng",
      width: 120,
      sortable: true,
      render: (m) => (
        <span className={`font-semibold ${TIER_META[m.tier]?.tone ?? "text-muted"}`}>
          {TIER_META[m.tier]?.label ?? m.tier}
        </span>
      ),
    },
    {
      key: "multiplier",
      label: "Nhân điểm",
      width: 100,
      align: "right",
      render: (m) => <span className="tnum font-semibold">{m.multiplier.toFixed(1)}x</span>,
    },
    {
      key: "expiresAt",
      label: "Hết hạn",
      width: 140,
      render: (m) =>
        m.expiresAt ? (
          <span className={m.lapsed ? "text-state-danger" : "text-muted"}>
            {formatDay(m.expiresAt)}
          </span>
        ) : (
          <span className="text-muted">—</span>
        ),
    },
    {
      key: "status",
      label: "Trạng thái",
      width: 120,
      render: (m) =>
        <span className={m.lapsed ? "text-state-danger" : "text-primary"}>{m.lapsed ? "Đã hết hạn" : m.tier === "None" ? "Khách thường" : "Còn hiệu lực"}</span>,
    },
    { key: "actions", label: "", width: 220, render: (m) => <div className="flex gap-1"><Button size="xs" variant="ghost" onClick={() => { setTargetUser(String(m.id)); setTier(m.tier); setAssignOpen(true); assign.reset(); }}>Đổi / gia hạn</Button><Button size="xs" variant="ghost" onClick={() => setRecovery(m)}>Khôi phục</Button></div> },
  ];

  return (
    <div>
      {recovery && <IssueRecoveryCode userId={recovery.id} name={recovery.fullName} onClose={() => setRecovery(null)} />}
      <Toolbar icon={Crown}
        title="Membership"
        subtitle="Quản lý hạng thành viên, gia hạn và hệ số tích điểm"
        actions={
          <>
            <Button variant="ghost" size="sm" onClick={() => setAssignOpen((v) => !v)}>
              <Crown className="h-4 w-4" /> Gán hạng
            </Button>
            <Button variant="ghost" size="sm" onClick={() => refetch()}>
              <RefreshCw className="h-4 w-4" />
            </Button>
          </>
        }
      />

      {(loadError || customersQuery.error) && <Callout tone="error">{(loadError ?? customersQuery.error)?.message}</Callout>}
      {assignOpen && (
        <Card className="mt-4 p-4">
          <div className="flex flex-wrap items-end gap-3">
            <div>
              <label className="mb-1 block text-xs font-medium text-muted" htmlFor="assign-user">
                Khách hàng
              </label>
              <input aria-label="Tìm khách" placeholder="Tên hoặc số điện thoại" value={search} onChange={(e) => setSearch(e.target.value)} className="mb-2 h-9 w-full rounded-lg border border-border px-3 text-sm" />
              <select id="assign-user" value={targetUser} onChange={(e) => setTargetUser(e.target.value)} className="h-9 rounded-lg border border-border bg-card px-3 text-sm">
                <option value="">Chọn khách hàng…</option>
                {customers.filter((c) => c.id === Number(targetUser) || `${c.fullName} ${c.phoneNumber}`.toLowerCase().includes(search.toLowerCase())).map((c) => <option key={c.id} value={c.id}>{c.fullName} · {c.phoneNumber}</option>)}
              </select>
            </div>
            <div>
              <label className="mb-1 block text-xs font-medium text-muted" htmlFor="assign-tier">
                Hạng
              </label>
              <select
                id="assign-tier"
                value={tier}
                onChange={(e) => setTier(e.target.value as MembershipTier)}
                className="h-9 rounded-lg border border-border bg-card px-3 text-sm outline-none focus:border-primary"
              >
                <option value="Silver">Silver (1.5x, 30 ngày)</option>
                <option value="Gold">Gold (2.0x, 90 ngày)</option>
                <option value="None">None (hủy membership)</option>
              </select>
            </div>
            <Button size="sm" disabled={assign.isPending || !targetUser} onClick={() => assign.mutate()}>
              <Medal className="h-4 w-4" /> Gán
            </Button>
          </div>
          {err && <Callout tone="error" className="mt-3">{err}</Callout>}
        </Card>
      )}

      <div className="mt-4">
        {isLoading && !members.length ? (
          <Busy label="Đang tải thành viên…" />
        ) : (
          <VirtualizedTable
            rows={members}
            columns={columns}
            rowKey={(m) => m.id}
            maxHeight={520}
            emptyLabel="Chưa có thành viên. Dùng “Gán hạng” để thêm."
          />
        )}
      </div>
    </div>
  );
}
