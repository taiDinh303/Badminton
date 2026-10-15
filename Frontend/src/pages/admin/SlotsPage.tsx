import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Plus, Trash2, Pencil } from "lucide-react";
import { PageHeader, Card, Busy, Callout, Button, Field } from "../../components/ui";
import { api, type TimeSlot } from "../../lib/api";
import { VirtualizedTable, type Column } from "../../components/VirtualizedTable";
import { clsx } from "clsx";

interface SlotForm {
  label: string;
  startTime: string;
  endTime: string;
  isPeak: boolean;
  isActive: boolean;
}

const emptyForm = (): SlotForm => ({
  label: "", startTime: "06:00", endTime: "07:00", isPeak: false, isActive: true,
});

/**
 * Admin "Khung giờ" (docs 07 §4).
 * Backend: CREATE hỗ trợ label+giờ+peak; UPDATE chỉ đổi label + isActive
 * (đổi giờ/peak = xóa rồi tạo mới). Form edit chỉ chỉnh label + trạng thái.
 */
export default function SlotsPage() {
  const [form, setForm] = useState<SlotForm | null>(null);
  const [editId, setEditId] = useState<number | null>(null);

  const { data: slots = [], isFetching, refetch } = useQuery({
    queryKey: ["time-slots"],
    queryFn: api.timeSlots.list,
  });

  const create = useMutation({
    mutationFn: (f: SlotForm) =>
      api.timeSlots.create({
        label: f.label, startTime: f.startTime, endTime: f.endTime, isPeak: f.isPeak,
      }),
    onSuccess: () => { setForm(null); setEditId(null); refetch(); },
  });
  const update = useMutation({
    mutationFn: ({ id, form: f }: { id: number; form: SlotForm }) =>
      api.timeSlots.update(id, { label: f.label, isActive: f.isActive }),
    onSuccess: () => { setForm(null); setEditId(null); refetch(); },
  });
  const del = useMutation({
    mutationFn: (id: number) => api.timeSlots.delete(id),
    onSuccess: () => refetch(),
  });

  const err =
    (create.error as Error | undefined)?.message ??
    (update.error as Error | undefined)?.message ??
    (del.error as Error | undefined)?.message ?? null;

  function openEdit(s: TimeSlot) {
    setEditId(s.id);
    setForm({
      label: s.label, startTime: s.startTime, endTime: s.endTime,
      isPeak: s.isPeak, isActive: s.isActive,
    });
  }

  function submit() {
    if (!form) return;
    if (editId == null) create.mutate(form);
    else update.mutate({ id: editId, form });
  }

  const columns: Column<TimeSlot>[] = [
    { key: "id", label: "ID", width: 70, sortable: true, render: (s) => <span className="tnum text-muted">{s.id}</span> },
    { key: "label", label: "Khung giờ", width: 160, sortable: true, render: (s) => <span className="font-medium tnum">{s.label}</span> },
    { key: "time", label: "Bắt đầu – Kết thúc", width: 200, render: (s) => <span className="tnum text-muted">{s.startTime} – {s.endTime}</span> },
    {
      key: "isPeak", label: "Loại giờ", width: 120,
      render: (s) => (
        <span className={clsx("rounded-full px-2 py-0.5 text-xs",
          s.isPeak ? "bg-accent/20 text-fg" : "bg-bg text-muted")}>
          {s.isPeak ? "Cao điểm" : "Giờ thường"}
        </span>
      ),
    },
    {
      key: "active", label: "Trạng thái", width: 120,
      render: (s) => (
        <span className={clsx("rounded-full px-2 py-0.5 text-xs",
          s.isActive ? "bg-slot-free-bg text-slot-free-fg" : "bg-slot-maint-bg text-slot-maint-fg")}>
          {s.isActive ? "Đang hoạt động" : "Tắt"}
        </span>
      ),
    },
    {
      key: "actions", label: "", width: 180, align: "right",
      render: (s) => (
        <div className="flex gap-2">
          <Button variant="ghost" className="h-8 min-h-0 px-2 text-xs" onClick={() => openEdit(s)}>
            <Pencil className="h-3 w-3" /> Sửa
          </Button>
          <Button variant="danger" className="h-8 min-h-0 px-2 text-xs" onClick={() => del.mutate(s.id)}>
            <Trash2 className="h-3 w-3" /> Xóa
          </Button>
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Khung giờ"
        subtitle="Tạo / chỉnh sửa khung giờ cho cả cơ sở"
        right={
          <Button variant="accent" onClick={() => { setEditId(null); setForm(emptyForm()); }}>
            <Plus className="h-4 w-4" /> Thêm khung giờ
          </Button>
        }
      />

      {err && <Callout tone="error" className="mt-3">{err}</Callout>}

      <div className="mt-4">
        {isFetching && slots.length === 0 ? (
          <Busy label="Đang tải khung giờ…" />
        ) : (
          <VirtualizedTable rows={slots} columns={columns} rowKey={(s) => s.id}
            maxHeight={480} emptyLabel="Chưa có khung giờ nào." />
        )}
      </div>

      {form && (
        <Card className="mt-5 border-primary p-4">
          <p className="mb-3 font-semibold">
            {editId == null ? "Thêm khung giờ" : "Sửa khung giờ (chỉ chỉnh nhãn + trạng thái)"}
          </p>
          {editId == null ? (
            <div className="grid gap-3 sm:grid-cols-4">
              <Field label="Nhãn (vd 06:00)" value={form.label}
                onChange={(e) => setForm({ ...form, label: e.target.value })} />
              <Field label="Giờ bắt đầu" type="time" value={form.startTime}
                onChange={(e) => setForm({ ...form, startTime: e.target.value })} />
              <Field label="Giờ kết thúc" type="time" value={form.endTime}
                onChange={(e) => setForm({ ...form, endTime: e.target.value })} />
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={form.isPeak}
                  onChange={(e) => setForm({ ...form, isPeak: e.target.checked })}
                  className="h-4 w-4 accent-primary" />
                Cao điểm
              </label>
            </div>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label="Nhãn" value={form.label}
                onChange={(e) => setForm({ ...form, label: e.target.value })} />
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={form.isActive}
                  onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                  className="h-4 w-4 accent-primary" />
                Đang hoạt động
              </label>
            </div>
          )}
          <div className="mt-4 flex gap-2">
            <Button variant="accent" disabled={create.isPending || update.isPending} onClick={submit}>
              {create.isPending || update.isPending ? "Đang lưu…" : editId == null ? "Thêm" : "Lưu"}
            </Button>
            <Button variant="ghost" onClick={() => { setForm(null); setEditId(null); }}>Hủy</Button>
          </div>
        </Card>
      )}
    </div>
  );
}
