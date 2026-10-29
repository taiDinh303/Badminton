import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Plus, Ticket, Lock } from "lucide-react";
import {
  Toolbar,
  Busy,
  Callout,
  Button,
  Field,
  SelectField,
  Modal,
  StatusBadge,
  ProgressBar,
} from "../../../components/ui";
import { api, type Voucher } from "../../../services/api";
import { formatDay, formatVnd } from "../../../utils/format";
import { isAdmin, useAuth } from "../../../store/auth";
import { VirtualizedTable, type Column } from "../../../components/VirtualizedTable";

interface VoucherForm {
  code: string;
  type: "Percent" | "Fixed";
  value: number;
  maxValue: number;
  minSpend: number;
  totalQty: number;
  validUntil: string;
}

const emptyForm = (): VoucherForm => ({
  code: "", type: "Percent", value: 10, maxValue: 50000, minSpend: 0, totalQty: 1000,
  validUntil: new Date(Date.now() + 30 * 864e5).toISOString().slice(0, 10),
});

export default function VouchersPage() {
  const [form, setForm] = useState<VoucherForm | null>(null);
  // Voucher create/deactivate are [Admin] in the backend; a Manager can only list.
  const canManage = isAdmin(useAuth((s) => s.user));

  const { data: vouchers = [], isLoading, refetch } = useQuery({
    queryKey: ["vouchers"],
    queryFn: () => api.vouchers.list(false),
  });

  const create = useMutation({
    mutationFn: (f: VoucherForm) =>
      api.vouchers.create({
        code: f.code.toUpperCase(),
        type: f.type,
        value: f.value,
        maxValue: f.type === "Percent" ? f.maxValue || null : undefined,
        minSpend: f.minSpend,
        totalQty: f.totalQty,
        validFrom: new Date().toISOString().slice(0, 10),
        validUntil: f.validUntil,
      }),
    onSuccess: () => { setForm(null); refetch(); },
  });
  const deactivate = useMutation({
    mutationFn: (id: number) => api.vouchers.deactivate(id),
    onSuccess: () => refetch(),
  });

  const err =
    (create.error as Error | undefined)?.message ??
    (deactivate.error as Error | undefined)?.message ?? null;

  const columns: Column<Voucher>[] = [
    {
      key: "code",
      label: "Mã",
      width: 150,
      sortable: true,
      render: (v) => (
        <span className="inline-flex items-center gap-1.5">
          <Ticket className="h-3.5 w-3.5 text-primary" />
          <span className="font-mono text-xs font-bold tracking-wide">{v.code}</span>
        </span>
      ),
    },
    {
      key: "type",
      label: "Loại",
      width: 120,
      render: (v) =>
        v.type === "Percent" ? (
          <span className="font-semibold text-primary">{v.value}%</span>
        ) : (
          <span className="font-semibold text-primary">{formatVnd(v.value)}</span>
        ),
    },
    {
      key: "minSpend",
      label: "Tối thiểu",
      width: 120,
      align: "right",
      render: (v) => <span className="text-muted tnum">{formatVnd(v.minSpend)}</span>,
    },
    {
      key: "used",
      label: "Đã dùng",
      width: 180,
      render: (v) => (
        <div className="w-40">
          <div className="mb-1 text-xs tnum text-muted">
            {v.usedQty} / {v.totalQty}
          </div>
          <ProgressBar value={v.usedQty} max={Math.max(1, v.totalQty)} />
        </div>
      ),
    },
    {
      key: "validUntil",
      label: "Hết hạn",
      width: 120,
      render: (v) => (
        <span className="text-muted tnum">{v.validUntil ? formatDay(v.validUntil) : "Vĩnh viễn"}</span>
      ),
    },
    {
      key: "state",
      label: "Trạng thái",
      width: 120,
      render: (v) => (
        isExpired(v) ? <span className="text-xs font-semibold text-state-danger">Hết hạn sử dụng</span> : <StatusBadge status={v.isActive ? "Open" : "Locked"} size="sm" />
      ),
    },
    {
      key: "actions",
      label: "",
      width: 120,
      align: "right",
      render: (v) => (
        canManage ? (
          v.isActive && !isExpired(v) ? (
            <Button
              variant="ghost"
              size="xs"
              onClick={() => deactivate.mutate(v.id)}
            >
              <Lock className="h-3 w-3" /> Khóa
            </Button>
          ) : (
            <span className="inline-flex items-center gap-1 text-xs text-muted">
              <Lock className="h-3 w-3" /> Đã khóa
            </span>
          )
        ) : (
          <span className="text-xs text-muted">—</span>
        )
      ),
    },
  ];

  return (
    <div>
      <Toolbar icon={Ticket}
        title="Voucher"
        subtitle="Mã giảm giá cho khách hàng"
        actions={
          canManage ? (
            <Button variant="accent" size="sm" onClick={() => setForm(emptyForm())}>
              <Plus className="h-4 w-4" /> Tạo voucher
            </Button>
          ) : undefined
        }
      />

      {err && <Callout tone="error" className="mt-3">{err}</Callout>}

      <div className="mt-4">
        {isLoading && vouchers.length === 0 ? (
          <Busy label="Đang tải voucher…" />
        ) : (
          <VirtualizedTable
            rows={vouchers}
            columns={columns}
            rowKey={(v) => v.id}
            maxHeight={480}
            emptyLabel="Chưa có voucher nào."
          />
        )}
      </div>

      {form && (
        <Modal
          title="Voucher mới"
          onClose={() => setForm(null)}
          width="max-w-2xl"
          footer={
            <>
              <Button variant="ghost" size="sm" onClick={() => setForm(null)}>
                Hủy
              </Button>
              <Button variant="accent" size="sm" disabled={create.isPending} onClick={() => create.mutate(form)}>
                {create.isPending ? "Đang tạo…" : "Tạo voucher"}
              </Button>
            </>
          }
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <Field
              label="Mã voucher"
              value={form.code}
              onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
              placeholder="VD: RAMDien10"
            />
            <SelectField
              label="Loại"
              value={form.type}
              onChange={(e) => setForm({ ...form, type: e.target.value as "Percent" | "Fixed" })}
            >
              <option value="Percent">Phần trăm (%)</option>
              <option value="Fixed">Cố định (VNĐ)</option>
            </SelectField>
            <Field
              label={form.type === "Percent" ? "Giảm (%)" : "Giảm (VNĐ)"}
              type="number"
              min={0}
              value={form.value}
              onChange={(e) => setForm({ ...form, value: Number(e.target.value) })}
            />
            {form.type === "Percent" && (
              <Field
                label="Trần giảm tối đa (VNĐ, có thể bỏ)"
                type="number"
                min={0}
                value={form.maxValue}
                onChange={(e) => setForm({ ...form, maxValue: Number(e.target.value) })}
              />
            )}
            <Field
              label="Đơn tối thiểu (VNĐ)"
              type="number"
              min={0}
              value={form.minSpend}
              onChange={(e) => setForm({ ...form, minSpend: Number(e.target.value) })}
            />
            <Field
              label="Số lượng phát hành"
              type="number"
              min={1}
              value={form.totalQty}
              onChange={(e) => setForm({ ...form, totalQty: Number(e.target.value) })}
            />
            <Field
              label="Hết hạn"
              type="date"
              value={form.validUntil}
              onChange={(e) => setForm({ ...form, validUntil: e.target.value })}
            />
          </div>
        </Modal>
      )}
    </div>
  );
}

function isExpired(v: Voucher) {
  return !!v.validUntil && v.validUntil.slice(0, 10) < new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh", year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date());
}
