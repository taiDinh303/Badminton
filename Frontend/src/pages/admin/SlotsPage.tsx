import { useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Plus, Trash2, Pencil, Clock } from "lucide-react";
import {
  Toolbar,
  Busy,
  Callout,
  Button,
  Field,
  Modal,
  StatusBadge,
} from "../../components/ui";
import { api, type TimeSlot } from "../../lib/api";
import { clsx } from "clsx";
import { isAdmin, useAuth } from "../../store/auth";
import { VirtualizedTable, type Column } from "../../components/VirtualizedTable";

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
  // Slot delete is [Admin] in the backend; create/update are Manager+.
  const canDelete = isAdmin(useAuth((s) => s.user));

  const { data: rawSlots = [], isFetching, refetch } = useQuery({
    queryKey: ["time-slots"],
    queryFn: api.timeSlots.list,
  });

  // Docs 07 §4: order slots chronologically so a re-created slot lands in
  // its time-of-day position instead of appended at the end.
  const slots = useMemo(
    () =>
      [...rawSlots].sort(
        (a, b) =>
          a.startTime.localeCompare(b.startTime) ||
          a.endTime.localeCompare(b.endTime) ||
          a.label.localeCompare(b.label),
      ),
    [rawSlots],
  );

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
    {
      key: "label",
      label: "Khung giờ",
      width: 160,
      sortable: true,
      render: (s) => (
        <span className="inline-flex items-center gap-1.5 font-semibold tnum">
          <Clock className="h-3.5 w-3.5 text-muted" />
          {s.label}
        </span>
      ),
    },
    { key: "time", label: "Bắt đầu – Kết thúc", width: 200, render: (s) => <span className="tnum text-muted">{s.startTime} – {s.endTime}</span> },
    {
      key: "isPeak",
      label: "Loại giờ",
      width: 130,
      render: (s) => (
        <span className={clsx("inline-flex rounded-full px-2.5 py-1 text-xs font-semibold",
          s.isPeak ? "bg-accent/25 text-fg" : "bg-bg text-muted")}>
          {s.isPeak ? "Cao điểm" : "Giờ thường"}
        </span>
      ),
    },
    {
      key: "active",
      label: "Trạng thái",
      width: 140,
      render: (s) => <StatusBadge status={s.isActive ? "Open" : "Closed"} size="sm" />,
    },
    {
      key: "actions",
      label: "",
      width: 180,
      align: "right",
      render: (s) => (
        <div className="flex gap-2">
          <Button variant="ghost" size="xs" onClick={() => openEdit(s)}>
            <Pencil className="h-3 w-3" /> Sửa
          </Button>
          {canDelete ? (
            <Button variant="danger" size="xs" onClick={() => del.mutate(s.id)}>
              <Trash2 className="h-3 w-3" /> Xóa
            </Button>
          ) : null}
        </div>
      ),
    },
  ];

  return (
    <div>
      <Toolbar
        title="Khung giờ"
        subtitle="Tạo / chỉnh sửa khung giờ cho cả cơ sở"
        actions={
          <Button variant="accent" size="sm" onClick={() => { setEditId(null); setForm(emptyForm()); }}>
            <Plus className="h-4 w-4" /> Thêm khung giờ
          </Button>
        }
      />

      {err && <Callout tone="error" className="mt-3">{err}</Callout>}

      <div className="mt-4">
        {isFetching && slots.length === 0 ? (
          <Busy label="Đang tải khung giờ…" />
        ) : (
          <VirtualizedTable
            rows={slots}
            columns={columns}
            rowKey={(s) => s.id}
            maxHeight={480}
            emptyLabel="Chưa có khung giờ nào."
          />
        )}
      </div>

      {form && (
        <Modal
          title={editId == null ? "Thêm khung giờ" : "Sửa khung giờ (chỉ nhãn + trạng thái)"}
          onClose={() => { setForm(null); setEditId(null); }}
          width="max-w-2xl"
          footer={
            <>
              <Button variant="ghost" size="sm" onClick={() => { setForm(null); setEditId(null); }}>
                Hủy
              </Button>
              <Button variant="accent" size="sm" disabled={create.isPending || update.isPending} onClick={submit}>
                {create.isPending || update.isPending ? "Đang lưu…" : editId == null ? "Thêm" : "Lưu"}
              </Button>
            </>
          }
        >
          {editId == null ? (
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Nhãn (vd 06:00)" value={form.label}
                onChange={(e) => setForm({ ...form, label: e.target.value })} />
              <div className="grid grid-cols-2 gap-3">
                <Field label="Giờ bắt đầu" type="time" value={form.startTime}
                  onChange={(e) => setForm({ ...form, startTime: e.target.value })} />
                <Field label="Giờ kết thúc" type="time" value={form.endTime}
                  onChange={(e) => setForm({ ...form, endTime: e.target.value })} />
              </div>
              <label className="flex items-center gap-2 text-sm sm:col-span-2">
                <input type="checkbox" checked={form.isPeak}
                  onChange={(e) => setForm({ ...form, isPeak: e.target.checked })}
                  className="h-4 w-4 accent-primary" />
                Giờ cao điểm
              </label>
            </div>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2">
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
        </Modal>
      )}
    </div>
  );
}
