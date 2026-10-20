import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Crown, Medal, RefreshCw } from "lucide-react";
import {
  StatusBadge,
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
  const [assignOpen, setAssignOpen] = useState(false);
  const [targetUser, setTargetUser] = useState("");
  const [tier, setTier] = useState<MembershipTier>("Silver");

  const { data: members = [], isFetching, refetch } = useQuery({
    queryKey: ["membership", "members"],
    queryFn: api.admin.membership.members,
  });

  const assign = useMutation({
    mutationFn: () => api.admin.membership.assign(Number(targetUser), tier),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["membership", "members"] });
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
        m.lapsed ? <StatusBadge status="Cancelled" size="sm" /> : m.tier === "None" ? <StatusBadge status="Pending" size="sm" /> : <StatusBadge status="Confirmed" size="sm" />,
    },
  ];

  return (
    <div>
      <Toolbar
        title="Membership"
        subtitle="Hành khách trả phí: Silver 1.5x · Gold 2.0x điểm"
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

      {assignOpen && (
        <Card className="mt-4 p-4">
          <div className="flex flex-wrap items-end gap-3">
            <div>
              <label className="mb-1 block text-xs font-medium text-muted" htmlFor="assign-user">
                ID khách
              </label>
              <input
                id="assign-user"
                type="number"
                min={1}
                value={targetUser}
                onChange={(e) => setTargetUser(e.target.value)}
                placeholder="VD: 3"
                className="h-9 w-28 rounded-lg border border-border bg-card px-3 text-sm outline-none focus:border-primary"
              />
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
        {isFetching && !members.length ? (
          <Busy label="Đang tải thành viên…" />
        ) : (
          <VirtualizedTable
            rows={members}
            columns={columns}
            rowKey={(m) => m.id}
            maxHeight={520}
            emptyLabel="Chưa có thành viên trả phí. Dùng “Gán hạng” để thêm."
          />
        )}
      </div>
    </div>
  );
}
