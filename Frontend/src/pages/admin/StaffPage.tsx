import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Plus, Lock, LockOpen } from "lucide-react";
import { PageHeader, Card, Busy, Callout, Button, Field } from "../../components/ui";
import { api, type Staff } from "../../lib/api";
import { VirtualizedTable, type Column } from "../../components/VirtualizedTable";
import { clsx } from "clsx";

interface StaffForm {
  fullName: string;
  phoneNumber: string;
  password: string;
  email: string;
  role: "Manager" | "Admin";
  branchId: number | null;
}

const emptyForm = (): StaffForm => ({
  fullName: "", phoneNumber: "", password: "", email: "",
  role: "Manager", branchId: null,
});

/**
 * Admin "Nhân viên" (docs 07 §4, chỉ Admin): list staff, tạo,
 * khóa/mở khóa, đổi role, gán cơ sở.
 */
export default function StaffPage() {
  const [form, setForm] = useState<StaffForm | null>(null);
  const { data: branches = [] } = useQuery({
    queryKey: ["branches"],
    queryFn: api.branches.list,
  });
  const { data: staff = [], isFetching, refetch } = useQuery({
    queryKey: ["staff"],
    queryFn: api.admin.staff.list,
  });

  const create = useMutation({
    mutationFn: (f: StaffForm) =>
      api.admin.staff.create({
        fullName: f.fullName, phoneNumber: f.phoneNumber,
        password: f.password, email: f.email || undefined,
        role: f.role, branchId: f.branchId ?? undefined,
      }),
    onSuccess: () => { setForm(null); refetch(); },
  });
  const lock = useMutation({
    mutationFn: (id: number) => api.admin.staff.lock(id),
    onSuccess: () => refetch(),
  });
  const unlock = useMutation({
    mutationFn: (id: number) => api.admin.staff.unlock(id),
    onSuccess: () => refetch(),
  });
  const changeRole = useMutation({
    mutationFn: ({ id, role }: { id: number; role: string }) =>
      api.admin.staff.changeRole(id, role),
    onSuccess: () => refetch(),
  });
  const assignBranch = useMutation({
    mutationFn: ({ id, branchId }: { id: number; branchId: number }) =>
      api.admin.staff.assignBranch(id, branchId),
    onSuccess: () => refetch(),
  });

  const err =
    (create.error as Error | undefined)?.message ??
    (lock.error as Error | undefined)?.message ??
    (changeRole.error as Error | undefined)?.message ??
    (assignBranch.error as Error | undefined)?.message ?? null;

  const branchName = (id: number | null) =>
    id == null ? "—" : branches.find((b) => b.id === id)?.name ?? `#${id}`;

  const columns: Column<Staff>[] = [
    { key: "fullName", label: "Họ tên", width: 160, sortable: true, render: (s) => <span className="font-medium">{s.fullName}</span> },
    { key: "phoneNumber", label: "SĐT", width: 130, render: (s) => <span className="tnum text-muted">{s.phoneNumber}</span> },
    {
      key: "role", label: "Vai trò", width: 100, sortable: true,
      render: (s) => (
        <span className={clsx("rounded-full px-2 py-0.5 text-xs font-medium",
          s.role === "Admin" ? "bg-primary/10 text-primary" : "bg-bg text-muted")}>
          {s.role}
        </span>
      ),
    },
    {
      key: "status", label: "Trạng thái", width: 110,
      render: (s) => (
        <span className={clsx("rounded-full px-2 py-0.5 text-xs",
          s.status === "Active" ? "bg-slot-free-bg text-slot-free-fg" : "bg-slot-booked-bg text-slot-booked-fg")}>
          {s.status === "Active" ? "Đang hoạt động" : "Đã khóa"}
        </span>
      ),
    },
    { key: "branch", label: "Cơ sở", width: 160, render: (s) => <span className="text-muted">{branchName(s.branchId)}</span> },
    {
      key: "actions", label: "", width: 220, align: "right",
      render: (s) => (
        <div className="flex items-center justify-end gap-1">
          <select
            aria-label={`Cơ sở của ${s.fullName}`}
            value={s.branchId ?? ""}
            onChange={(e) => e.target.value && assignBranch.mutate({ id: s.id, branchId: Number(e.target.value) })}
            className="max-w-32 rounded border border-border bg-card px-1.5 py-1 text-xs text-muted"
          >
            <option value="">Gán cơ sở…</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </select>
          <select
            aria-label={`Vai trò của ${s.fullName}`}
            value={s.role}
            onChange={(e) => changeRole.mutate({ id: s.id, role: e.target.value })}
            className="rounded border border-border bg-card px-1.5 py-1 text-xs text-muted"
          >
            <option value="Manager">Manager</option>
            <option value="Admin">Admin</option>
          </select>
          <button
            aria-label={s.status === "Active" ? "Khóa tài khoản" : "Mở khóa"}
            onClick={() => s.status === "Active" ? lock.mutate(s.id) : unlock.mutate(s.id)}
            className={clsx("flex h-8 w-8 items-center justify-center rounded border text-xs",
              s.status === "Active" ? "border-border text-muted hover:text-state-error" : "border-state-success text-state-success")}
          >
            {s.status === "Active" ? <Lock className="h-3.5 w-3.5" /> : <LockOpen className="h-3.5 w-3.5" />}
          </button>
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="Nhân viên"
        subtitle="Quản lý tài khoản Manager / Admin"
        right={
          <Button variant="accent" onClick={() => setForm(emptyForm())}>
            <Plus className="h-4 w-4" /> Tạo nhân viên
          </Button>
        }
      />

      {err && <Callout tone="error" className="mt-3">{err}</Callout>}

      <div className="mt-4">
        {isFetching && staff.length === 0 ? (
          <Busy label="Đang tải nhân viên…" />
        ) : (
          <VirtualizedTable rows={staff} columns={columns} rowKey={(s) => s.id}
            maxHeight={460} emptyLabel="Chưa có nhân viên nào." />
        )}
      </div>

      {form && (
        <Card className="mt-5 border-primary p-4">
          <p className="mb-3 font-semibold">Nhân viên mới</p>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Họ và tên" value={form.fullName}
              onChange={(e) => setForm({ ...form, fullName: e.target.value })} required />
            <Field label="Số điện thoại" value={form.phoneNumber}
              onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })}
              inputMode="tel" required />
            <Field label="Mật khẩu" type="password" value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })} required />
            <Field label="Email (tuỳ chọn)" value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })} type="email" />
            <label className="text-sm">
              <span className="mb-1 block text-muted">Vai trò</span>
              <select value={form.role}
                onChange={(e) => setForm({ ...form, role: e.target.value as "Manager" | "Admin" })}
                className="w-full rounded-lg border border-border bg-card px-3 py-2 outline-none focus:border-primary">
                <option value="Manager">Manager</option>
                <option value="Admin">Admin</option>
              </select>
            </label>
            <label className="text-sm">
              <span className="mb-1 block text-muted">Cơ sở (tuỳ chọn)</span>
              <select value={form.branchId ?? ""}
                onChange={(e) => setForm({ ...form, branchId: e.target.value ? Number(e.target.value) : null })}
                className="w-full rounded-lg border border-border bg-card px-3 py-2 outline-none focus:border-primary">
                <option value="">Không gán</option>
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>{b.name}</option>
                ))}
              </select>
            </label>
          </div>
          <div className="mt-4 flex gap-2">
            <Button variant="accent" disabled={create.isPending}
              onClick={() => create.mutate(form)}>
              {create.isPending ? "Đang tạo…" : "Tạo nhân viên"}
            </Button>
            <Button variant="ghost" onClick={() => setForm(null)}>Hủy</Button>
          </div>
        </Card>
      )}
    </div>
  );
}
