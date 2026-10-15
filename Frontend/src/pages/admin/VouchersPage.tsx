import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Plus, Trash2 } from "lucide-react";
import { PageHeader, Card, Busy, Callout, Button, Field } from "../../components/ui";
import { api, formatVnd, type Voucher } from "../../lib/api";
import { VirtualizedTable, type Column } from "../../components/VirtualizedTable";

interface VoucherForm {
  code: string;
  type: "Percent" | "Fixed";
  value: number;
  minSpend: number;
  totalQty: number;
  validUntil: string;
}

const emptyForm = (): VoucherForm => ({
  code: "", type: "Percent", value: 10, minSpend: 0, totalQty: 1000,
  validUntil: new Date(Date.now() + 30 * 864e5).toISOString().slice(0, 10),
});

/**
 * Admin "Voucher" (docs 07 §4): paged/virtualized list + create + deactivate.
 */
export default function VouchersPage() {
  const [form, setForm] = useState<VoucherForm | null>(null);

  const { data: vouchers = [], isFetching, refetch } = useQuery({
    queryKey: ["vouchers"],
    queryFn: () => api.vouchers.list(true),
  });

  const create = useMutation({
    mutationFn: (f: VoucherForm) =>
      api.vouchers.create({
        code: f.code.toUpperCase(), type: f.type, value: f.value,
        minSpend: f.minSpend, totalQty: f.totalQty,
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
    { key: "code", label: "Mã", width: 140, sortable: true, render: (v) => <span className="font-mono text-xs font-semibold">{v.code}</span> },
    { key: "type", label: "Loại", width: 100, render: (v) => (v.type === "Percent" ? `${v.value}%` : formatVnd(v.value)) },
    { key: "minSpend", label: "Tối thiểu", width: 120, align: "right", render: (v) => <span className="tnum text-muted">{formatVnd(v.minSpend)}</span> },
    { key: "used", label: "Đã dùng", width: 120, align: "right", render: (v) => <span className="tnum">{v.usedQty} / {v.totalQty}</span> },
    { key: "validUntil", label: "Hết hạn", width: 120, render: (v) => <span className="text-muted">{v.validUntil ?? "Vĩnh viễn"}</span> },
    {
      key: "actions", label: "", width: 90, align: "right",
      render: (v) => (
        <Button variant="danger" className="h-8 min-h-0 px-2 text-xs" onClick={() => deactivate.mutate(v.id)} disabled={!v.isActive}>
          <Trash2 className="h-3 w-3" /> Vô hiệu
        </Button>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Voucher"
        subtitle="Mã giảm giá cho khách hàng"
        right={
          <Button variant="accent" onClick={() => setForm(emptyForm())}>
            <Plus className="h-4 w-4" /> Tạo voucher
          </Button>
        }
      />

      {err && <Callout tone="error" className="mt-3">{err}</Callout>}

      <div className="mt-4">
        {isFetching && vouchers.length === 0 ? (
          <Busy label="Đang tải voucher…" />
        ) : (
          <VirtualizedTable rows={vouchers} columns={columns} rowKey={(v) => v.id} maxHeight={480}
            emptyLabel="Chưa có voucher nào." />
        )}
      </div>

      {form && (
        <Card className="mt-5 border-primary p-4">
          <p className="mb-3 font-semibold">Voucher mới</p>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Mã voucher" value={form.code}
              onChange={(e) => setForm({ ...form, code: e.target.value.toUpperCase() })}
              placeholder="VD: RAMDien10" />
            <label className="text-sm">
              <span className="mb-1 block text-muted">Loại</span>
              <select value={form.type}
                onChange={(e) => setForm({ ...form, type: e.target.value as "Percent" | "Fixed" })}
                className="w-full rounded-lg border border-border bg-card px-3 py-2 outline-none focus:border-primary">
                <option value="Percent">Phần trăm (%)</option>
                <option value="Fixed">Cố định (VNĐ)</option>
              </select>
            </label>
            <Field label={form.type === "Percent" ? "Giảm (%)" : "Giảm (VNĐ)"} type="number" min={0}
              value={form.value}
              onChange={(e) => setForm({ ...form, value: Number(e.target.value) })} />
            <Field label="Đơn tối thiểu (VNĐ)" type="number" min={0}
              value={form.minSpend}
              onChange={(e) => setForm({ ...form, minSpend: Number(e.target.value) })} />
            <Field label="Số lượng phát hành" type="number" min={1}
              value={form.totalQty}
              onChange={(e) => setForm({ ...form, totalQty: Number(e.target.value) })} />
            <Field label="Hết hạn" type="date" value={form.validUntil}
              onChange={(e) => setForm({ ...form, validUntil: e.target.value })} />
          </div>
          <div className="mt-4 flex gap-2">
            <Button variant="accent" disabled={create.isPending}
              onClick={() => create.mutate(form)}>
              {create.isPending ? "Đang tạo…" : "Tạo voucher"}
            </Button>
            <Button variant="ghost" onClick={() => setForm(null)}>Hủy</Button>
          </div>
        </Card>
      )}
    </div>
  );
}
