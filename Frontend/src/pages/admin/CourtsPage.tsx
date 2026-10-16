import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Pencil, Plus, Trash2, Star, Dumbbell } from "lucide-react";
import {
  Toolbar,
  Card,
  Busy,
  Callout,
  Button,
  Field,
  SelectField,
  Modal,
  StatusBadge,
} from "../../components/ui";
import { api, formatVnd, type Court } from "../../lib/api";

interface CourtForm {
  branchId: number;
  code: string;
  name: string;
  priceLowPeak: number;
  pricePeak: number;
  weekendSurcharge: number; // percent 0-100
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
 * Admin "Sân & giá" (docs 07 §4): card grid of all courts (giá peak/low,
 * phụ thu, sức chứa, rating) + form modal (tạo / sửa).
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
      <Toolbar
        title="Sân & giá"
        subtitle="Quản lý sân, giá giờ thường / cao điểm, phụ thu cuối tuần"
        actions={
          <Button variant="accent" size="sm" onClick={() => { setEditId(null); setForm(emptyForm()); }}>
            <Plus className="h-4 w-4" /> Thêm sân
          </Button>
        }
      />

      {err && <Callout tone="error" className="mt-3">{err}</Callout>}

      {isFetching && courts.length === 0 ? (
        <Busy label="Đang tải danh sách sân…" />
      ) : (
        <div className="mt-5 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {courts.map((c) => (
            <Card key={c.id} className="flex flex-col p-4">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
                      <Dumbbell className="h-4 w-4" />
                    </span>
                    <div className="min-w-0">
                      <p className="truncate font-bold">{c.name}</p>
                      <p className="text-[11px] text-muted">{branchName(c.branchId)}</p>
                    </div>
                  </div>
                </div>
                <StatusBadge status={c.isActive ? "Open" : "Closed"} size="sm" />
              </div>

              {/* Price */}
              <div className="mt-4 grid grid-cols-2 gap-2">
                <div className="rounded-lg bg-bg p-2.5">
                  <p className="text-[11px] font-medium text-muted">Giờ thường</p>
                  <p className="text-sm font-bold tnum">{formatVnd(c.priceLowPeak)}</p>
                </div>
                <div className="rounded-lg bg-accent/15 p-2.5">
                  <p className="text-[11px] font-medium text-muted">Cao điểm</p>
                  <p className="text-sm font-bold tnum">{formatVnd(c.pricePeak)}</p>
                </div>
              </div>

              {/* Meta */}
              <div className="mt-3 flex items-center justify-between text-xs text-muted">
                <span>Phụ thu CN {Math.round((c.weekendSurcharge as number) * 100)}% · Sức chứa {c.capacity}</span>
                <span className="inline-flex items-center gap-1">
                  <Star className="h-3.5 w-3.5 text-accent" />
                  {c.ratingAvg != null ? c.ratingAvg.toFixed(1) : "Mới"}
                </span>
              </div>
              <p className="mt-1 text-[11px] text-muted">{c.surface} · Mã {c.code}</p>

              <div className="mt-4 flex gap-2 border-t border-border pt-3">
                <Button variant="ghost" size="xs" className="flex-1" onClick={() => openEdit(c)}>
                  <Pencil className="h-3 w-3" /> Chỉnh sửa
                </Button>
                <Button variant="danger" size="xs" className="flex-1" onClick={() => del.mutate(c.id)}>
                  <Trash2 className="h-3 w-3" /> Xóa
                </Button>
              </div>
            </Card>
          ))}
        </div>
      )}

      {form && (
        <Modal
          title={editId == null ? "Thêm sân mới" : "Chỉnh sửa sân"}
          onClose={() => { setForm(null); setEditId(null); }}
          width="max-w-2xl"
          footer={
            <>
              <Button variant="ghost" size="sm" onClick={() => { setForm(null); setEditId(null); }}>
                Hủy
              </Button>
              <Button variant="accent" size="sm" disabled={create.isPending || update.isPending} onClick={submit}>
                {create.isPending || update.isPending ? "Đang lưu…" : editId == null ? "Thêm sân" : "Lưu thay đổi"}
              </Button>
            </>
          }
        >
          <div className="grid gap-4 sm:grid-cols-2">
            {editId == null && (
              <SelectField label="Cơ sở" value={form.branchId || ""} onChange={(e) => setForm({ ...form, branchId: Number(e.target.value) })}>
                <option value="" disabled>Chọn cơ sở…</option>
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>{b.name}</option>
                ))}
              </SelectField>
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
            {editId != null && (
              <label className="flex items-center gap-2 text-sm sm:col-span-2">
                <input
                  type="checkbox"
                  checked={!!form.isActive}
                  onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                  className="h-4 w-4 accent-primary"
                />
                Sân đang hoạt động
              </label>
            )}
          </div>
        </Modal>
      )}
    </div>
  );
}
