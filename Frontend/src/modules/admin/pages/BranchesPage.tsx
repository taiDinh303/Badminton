import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Branch } from "../../../services/api";
import { Toolbar, Busy, Callout, Button, Field, Modal } from "../../../components/ui";

type Form = { id?: number; name: string; address: string; openTime: string; closeTime: string; isActive: boolean };
const empty = (): Form => ({ name: "", address: "", openTime: "06:00", closeTime: "23:00", isActive: true });

export default function BranchesPage() {
  const qc = useQueryClient();
  const [form, setForm] = useState<Form | null>(null);
  const branches = useQuery({ queryKey: ["branches"], queryFn: api.branches.list });
  const save = useMutation({
    mutationFn: async (f: Form) => f.id === undefined
      ? api.branches.create({ name: f.name.trim(), address: f.address.trim(), openTime: f.openTime, closeTime: f.closeTime })
      : api.branches.update(f.id, { name: f.name.trim(), address: f.address.trim(), openTime: f.openTime, closeTime: f.closeTime, isActive: f.isActive }),
    onSuccess: () => { setForm(null); void qc.invalidateQueries({ queryKey: ["branches"] }); void qc.invalidateQueries({ queryKey: ["availability"] }); },
  });
  const edit = (branch: Branch) => { save.reset(); setForm({ ...branch }); };
  return <div>
    <Toolbar title="Cơ sở" subtitle="Quản lý địa chỉ, giờ mở cửa và trạng thái hoạt động"
      actions={<Button onClick={() => { save.reset(); setForm(empty()); }}>Thêm cơ sở</Button>} />
    {branches.isPending && <Busy />}
    {branches.isError && <Callout tone="error">{branches.error.message}</Callout>}
    <div className="mt-4 grid gap-3 sm:grid-cols-2">
      {branches.data?.map(b => <div key={b.id} className="rounded-xl border border-border bg-card p-4">
        <h2 className="font-bold">{b.name}</h2><p className="mt-1 text-sm text-muted">{b.address}</p>
        <p className="my-2 text-sm">{b.openTime} – {b.closeTime} · {b.isActive ? "Đang hoạt động" : "Tạm ngưng"}</p>
        <Button size="sm" variant="ghost" onClick={() => edit(b)}>Sửa {b.name}</Button>
      </div>)}
    </div>
    {form && <Modal title={form.id === undefined ? "Thêm cơ sở" : "Sửa cơ sở"} onClose={() => setForm(null)} footer={
      <Button disabled={save.isPending || !form.name.trim() || !form.address.trim() || !form.openTime || form.closeTime <= form.openTime}
        onClick={() => save.mutate(form)}>{save.isPending ? "Đang lưu…" : "Lưu cơ sở"}</Button>
    }>
      <div className="grid gap-3">
        <Field label="Tên cơ sở" maxLength={100} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} />
        <Field label="Địa chỉ" maxLength={255} value={form.address} onChange={e => setForm({ ...form, address: e.target.value })} />
        <Field label="Giờ mở cửa" type="time" value={form.openTime} onChange={e => setForm({ ...form, openTime: e.target.value })} />
        <Field label="Giờ đóng cửa" type="time" value={form.closeTime} onChange={e => setForm({ ...form, closeTime: e.target.value })} />
        {form.id !== undefined && <label className="flex gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={e => setForm({ ...form, isActive: e.target.checked })} />Đang hoạt động</label>}
        {save.isError && <Callout tone="error">{save.error.message}</Callout>}
      </div>
    </Modal>}
  </div>;
}
