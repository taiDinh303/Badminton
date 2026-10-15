import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Pencil, Plus, Trash2 } from "lucide-react";
import { PageHeader, Card, Busy, Callout, Button, Field } from "../../components/ui";
import { api, formatVnd, type Court } from "../../lib/api";

interface CourtForm {
  branchId: number;
  code: string;
  name: string;
  priceLowPeak: number;
  pricePeak: number;
  weekendSurcharge: number; // stored as percent 0-100
  capacity: number;
  surface: string;
  isActive?: boolean;
}

const emptyForm = (): CourtForm => ({
  branchId: 0, code: "", name: "",
  priceLowPeak: 100000, pricePeak: 150000,
  weekendSurcharge: 20, capacity: 2, surface: "Sindalo",
});

/**
 * Admin "Sân & giá" (docs 07 §4): CRUD courts across branches,
 * card grid of all courts, form with price + capacity + surface.
 */
export default function CourtsPage() {
  const [form, setForm] = useState<CourtForm | null>(null);
  const [editId, setEditId] = useState<number | null>(null);

  const { data: branches = [] } = useQuery({
    queryKey: ["branches"],
    queryFn: api.branches.list,
  });
  const { data: courts = [], isFetching, refetch } = useQuery({
    queryKey: ["courts"],
    queryFn: () => api.courts.list(),
  });

  const create = useMutation({
    mutationFn: (f: CourtForm) =>
      api.courts.create({
        branchId: f.branchId, code: f.code, name: f.name,
        priceLowPeak: f.priceLowPeak, pricePeak: f.pricePeak,
        weekendSurcharge: f.weekendSurcharge / 100, capacity: f.capacity,
        surface: f.surface,
      }),
    onSuccess: () => { setForm(null); setEditId(null); refetch(); },
  });
  const update = useMutation({
    mutationFn: ({ id, form: f }: { id: number; form: CourtForm }) =>
      api.courts.update(id, {
        name: f.name, priceLowPeak: f.priceLowPeak, pricePeak: f.pricePeak,
        weekendSurcharge: f.weekendSurcharge / 100, capacity: f.capacity,
        surface: f.surface, isActive: f.isActive,
      }),
    onSuccess: () => { setForm(null); setEditId(null); refetch(); },
  });
  const del = useMutation({
    mutationFn: (id: number) => api.courts.delete(id),
    onSuccess: () => refetch(),
  });

  const branchName = (id: number) => branches.find((b) => b.id === id)?.name ?? `#${id}`;
  const err =
    (create.error as Error | undefined)?.message ??
    (update.error as Error | undefined)?.message ??
    (del.error as Error | undefined)?.message ?? null;

  function openEdit(c: Court) {
    setEditId(c.id);
    setForm({
      branchId: c.branchId, code: c.code, name: c.name,
      priceLowPeak: c.priceLowPeak, pricePeak: c.pricePeak,
      weekendSurcharge: Math.round((c.weekendSurcharge as number) * 100),
      capacity: c.capacity, surface: c.surface, isActive: c.isActive,
    });
  }

  function submit() {
    if (!form) return;
    if (editId == null) create.mutate(form);
    else update.mutate({ id: editId, form });
  }

  return (
    <div>
      <PageHeader
        title="Sân & giá"
        subtitle="Quản lý sân, giá giờ thường / cao điểm, phụ thu cuối tuần"
        right={
          <Button variant="accent" onClick={() => { setEditId(null); setForm(emptyForm()); }}>
            <Plus className="h-4 w-4" /> Thêm sân
          </Button>
        }
      />

      {err && <Callout tone="error" className="mt-3">{err}</Callout>}

      {isFetching && courts.length === 0 ? (
        <Busy label="Đang tải danh sách sân…" />
      ) : (
        <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {courts.map((c) => (
            <Card key={c.id} className="p-4">
              <div className="flex items-start justify-between">
                <div>
                  <p className="font-semibold">{c.name}</p>
                  <p className="text-xs text-muted">{branchName(c.branchId)}</p>
                </div>
                <span className={c.isActive ? "text-xs text-state-success" : "text-xs text-state-error"}>
                  {c.isActive ? "Đang hoạt động" : "Ngừng"}
                </span>
              </div>
              <div className="mt-3 flex items-center justify-between text-sm">
                <span className="text-muted">Giờ thường: <span className="tnum font-medium text-fg">{formatVnd(c.priceLowPeak)}</span></span>
                <span className="text-muted">Cao điểm: <span className="tnum font-medium text-fg">{formatVnd(c.pricePeak)}</span></span>
              </div>
              <p className="mt-1 text-xs text-muted">
                Phụ thu CN {Math.round((c.weekendSurcharge as number) * 100)}% · Sức chứa {c.capacity} · {c.surface}
              </p>
              <div className="mt-3 flex gap-2">
                <Button variant="ghost" className="h-8 min-h-0 px-2 text-xs" onClick={() => openEdit(c)}>
                  <Pencil className="h-3 w-3" /> Sửa
                </Button>
                <Button variant="danger" className="h-8 min-h-0 px-2 text-xs" onClick={() => del.mutate(c.id)}>
                  <Trash2 className="h-3 w-3" /> Vô hiệu
                </Button>
              </div>
            </Card>
          ))}
        </div>
      )}

      {form && (
        <Card className="mt-5 border-primary p-4">
          <p className="mb-3 font-semibold">{editId == null ? "Thêm sân mới" : "Chỉnh sửa sân"}</p>
          <div className="grid gap-3 sm:grid-cols-2">
            {editId == null && (
              <label className="text-sm">
                <span className="mb-1 block text-muted">Cơ sở</span>
                <select
                  value={form.branchId || ""}
                  onChange={(e) => setForm({ ...form, branchId: Number(e.target.value) })}
                  className="w-full rounded-lg border border-border bg-card px-3 py-2 outline-none focus:border-primary"
                >
                  <option value="" disabled>Chọn cơ sở…</option>
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>{b.name}</option>
                  ))}
                </select>
              </label>
            )}
            <Field label="Mã sân" value={form.code} disabled={editId != null}
              onChange={(e) => setForm({ ...form, code: e.target.value })} />
            <Field label="Tên sân" value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })} />
            <Field label="Sức chứa (người)" type="number" min={1} max={10}
              value={form.capacity}
              onChange={(e) => setForm({ ...form, capacity: Number(e.target.value) })} />
            <Field label="Giá giờ thường (VNĐ)" type="number" min={0}
              value={form.priceLowPeak}
              onChange={(e) => setForm({ ...form, priceLowPeak: Number(e.target.value) })} />
            <Field label="Giá giờ cao điểm (VNĐ)" type="number" min={0}
              value={form.pricePeak}
              onChange={(e) => setForm({ ...form, pricePeak: Number(e.target.value) })} />
            <Field label="Phụ thu cuối tuần (%, vd 20)" type="number" min={0} max={100}
              value={form.weekendSurcharge}
              onChange={(e) => setForm({ ...form, weekendSurcharge: Number(e.target.value) })} />
          </div>
          <div className="mt-4 flex gap-2">
            <Button variant="accent" disabled={create.isPending || update.isPending} onClick={submit}>
              {create.isPending || update.isPending ? "Đang lưu…" : editId == null ? "Thêm sân" : "Lưu"}
            </Button>
            <Button variant="ghost" onClick={() => { setForm(null); setEditId(null); }}>Hủy</Button>
          </div>
        </Card>
      )}
    </div>
  );
}
