import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Plus, Lock, LockOpen, Shield, MapPin } from "lucide-react";
import {
  Toolbar,
  Busy,
  Callout,
  Button,
  Field,
  SelectField,
  Modal,
  StatusBadge,
} from "../../../components/ui";
import { api, type Staff } from "../../../services/api";
import { VirtualizedTable, type Column } from "../../../components/VirtualizedTable";

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

export default function StaffPage() {
  const [form, setForm] = useState<StaffForm | null>(null);
  const { data: branches = [] } = useQuery({
    queryKey: ["branches"],
    queryFn: api.branches.list,
  });
  const { data: staff = [], isLoading, refetch, error: loadError } = useQuery({
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
    loadError?.message ??
    (create.error as Error | undefined)?.message ??
    (lock.error as Error | undefined)?.message ??
    (unlock.error as Error | undefined)?.message ??
    (changeRole.error as Error | undefined)?.message ??
    (assignBranch.error as Error | undefined)?.message ?? null;

  const branchName = (id: number | null) =>
    id == null ? "—" : branches.find((b) => b.id === id)?.name ?? `#${id}`;

  const columns: Column<Staff>[] = [
    {
      key: "fullName",
      label: "Họ tên",
      width: 200,
      sortable: true,
      render: (s) => (
        <div className="flex items-center gap-2.5">
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
            {s.fullName.charAt(0).toUpperCase()}
          </span>
          <div className="min-w-0">
            <p className="truncate text-sm font-semibold">{s.fullName}</p>
            <p className="text-[11px] text-muted tnum">{s.phoneNumber}</p>
          </div>
        </div>
      ),
    },
    {
      key: "role",
      label: "Vai trò",
      width: 130,
      sortable: true,
      render: (s) => (
        <span className="inline-flex items-center gap-1.5 rounded-full bg-bg px-2.5 py-1 text-xs font-semibold text-muted">
          <Shield className="h-3 w-3" />
          {s.role}
        </span>
      ),
    },
    {
      key: "status",
      label: "Trạng thái",
      width: 130,
      render: (s) => <StatusBadge status={s.status === "Active" ? "Active" : "Locked"} size="sm" />,
    },
    {
      key: "branch",
      label: "Cơ sở",
      width: 180,
      render: (s) => (
        <span className="inline-flex items-center gap-1 text-sm text-muted">
          <MapPin className="h-3.5 w-3.5" />
          {branchName(s.branchId)}
        </span>
      ),
    },
    {
      key: "actions",
      label: "",
      width: 280,
      align: "right",
      render: (s) => (
        <div className="flex items-center justify-end gap-2">
          <select
            aria-label={`Cơ sở của ${s.fullName}`}
            disabled={s.role === "Admin" || assignBranch.isPending}
            value={s.branchId ?? ""}
            onChange={(e) => e.target.value && assignBranch.mutate({ id: s.id, branchId: Number(e.target.value) })}
            className="h-8 max-w-[120px] rounded-lg border border-border bg-card px-2 text-xs text-muted outline-none focus:border-primary"
          >
            <option value="">Gán cơ sở…</option>
            {branches.map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </select>
          <select
            aria-label={`Vai trò của ${s.fullName}`}
            disabled={s.role === "Admin" || changeRole.isPending}
            value={s.role}
            onChange={(e) => changeRole.mutate({ id: s.id, role: e.target.value })}
            className="h-8 rounded-lg border border-border bg-card px-2 text-xs text-muted outline-none focus:border-primary"
          >
            <option value="Manager">Manager</option>
            {s.role === "Admin" && <option value="Admin">Admin</option>}
          </select>
          <button
            type="button"
            disabled={s.role === "Admin" || lock.isPending || unlock.isPending}
            aria-label={s.status === "Active" ? "Khóa tài khoản" : "Mở khóa"}
            onClick={() => (s.status === "Active" ? lock.mutate(s.id) : unlock.mutate(s.id))}
            className="flex h-8 w-8 items-center justify-center rounded-lg border border-border text-muted transition-colors hover:bg-bg hover:text-fg"
          >
            {s.status === "Active" ? <Lock className="h-3.5 w-3.5" /> : <LockOpen className="h-3.5 w-3.5 text-state-success" />}
          </button>
        </div>
      ),
    },
  ];

  return (
    <div>
      <Toolbar
        title="Nhân viên"
        subtitle="Quản lý tài khoản Manager / Admin"
        actions={
          <Button variant="accent" size="sm" onClick={() => setForm(emptyForm())}>
            <Plus className="h-4 w-4" /> Tạo nhân viên
          </Button>
        }
      />

      {err && <Callout tone="error" className="mt-3">{err}</Callout>}

      <div className="mt-4">
        {isLoading && staff.length === 0 ? (
          <Busy label="Đang tải nhân viên…" />
        ) : (
          <VirtualizedTable
            rows={staff}
            columns={columns}
            rowKey={(s) => s.id}
            rowHeight={56}
            maxHeight={460}
            emptyLabel="Chưa có nhân viên nào."
          />
        )}
      </div>

      {form && (
        <Modal
          title="Nhân viên mới"
          onClose={() => setForm(null)}
          width="max-w-2xl"
          footer={
            <>
              <Button variant="ghost" size="sm" onClick={() => setForm(null)}>
                Hủy
              </Button>
              <Button variant="accent" size="sm" disabled={create.isPending || !form.branchId || !/^0[0-9]{9}$/.test(form.phoneNumber) || form.password.length < 8 || form.fullName.trim().length < 2} onClick={() => create.mutate(form)}>
                {create.isPending ? "Đang tạo…" : "Tạo nhân viên"}
              </Button>
            </>
          }
        >
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Họ và tên" value={form.fullName}
              onChange={(e) => setForm({ ...form, fullName: e.target.value })} required />
            <Field label="Số điện thoại" value={form.phoneNumber}
              onChange={(e) => setForm({ ...form, phoneNumber: e.target.value })}
              inputMode="tel" required />
            <Field label="Mật khẩu" type="password" value={form.password}
              onChange={(e) => setForm({ ...form, password: e.target.value })} required />
            <Field label="Email (tuỳ chọn)" value={form.email}
              onChange={(e) => setForm({ ...form, email: e.target.value })} type="email" />
            <SelectField
              label="Vai trò"
              value={form.role}
              onChange={(e) => setForm({ ...form, role: e.target.value as "Manager" | "Admin" })}
            >
              <option value="Manager">Manager</option>
            </SelectField>
            <SelectField
              label="Cơ sở (bắt buộc)"
              value={form.branchId ?? ""}
              onChange={(e) => setForm({ ...form, branchId: e.target.value ? Number(e.target.value) : null })}
            >
              <option value="">Chọn cơ sở</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>{b.name}</option>
              ))}
            </SelectField>
          </div>
        </Modal>
      )}
    </div>
  );
}
