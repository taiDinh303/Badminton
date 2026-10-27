import { useState, type FormEvent, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api } from "../../../services/api";
import {
  Button,
  Callout,
  PageHeader,
  SelectField,
} from "../../../components/ui";
import { useAuth } from "../../../store/auth";
import { formatClubTime } from "../utils/clubTime";
export {
  Panel,
  Pages,
  ActionStatus,
} from "../../experience/components/ExperienceUi";
export { useExperienceAction as useClubAction } from "../../experience/hooks/useExperienceAction";
export const money = (value: number) =>
  new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(
    value,
  );
export const time = formatClubTime;
export const text = (form: FormData, name: string) =>
  String(form.get(name) ?? "");
export const num = (form: FormData, name: string) => Number(text(form, name));
export const utc = (form: FormData, name: string) =>
  new Date(`${text(form, name)}:00+07:00`).toISOString();
export function localDateTime(date = new Date()) {
  return new Date(date.getTime() + 7 * 3600000).toISOString().slice(0, 16);
}
export function useBranch() {
  const user = useAuth((s) => s.user);
  const [selected, setSelected] = useState(0);
  const query = useQuery({
    queryKey: ["branches"],
    queryFn: api.branches.list,
  });
  const branches = query.data ?? [];
  const branchId = selected || branches[0]?.id || 0;
  const control = (
    <>
      <SelectField
        label="Cơ sở"
        value={branchId}
        onChange={(e) => setSelected(Number(e.target.value))}
      >
        <option value={0}>Chọn cơ sở</option>
        {branches.map((b) => (
          <option key={b.id} value={b.id}>
            {b.name}
          </option>
        ))}
      </SelectField>
      <QueryStatus query={query} />
    </>
  );
  return { branchId, control, user };
}
export function Shell({
  title,
  subtitle,
  children,
  staff = false,
}: {
  title: string;
  subtitle: string;
  children: ReactNode;
  staff?: boolean;
}) {
  return (
    <div className="mx-auto max-w-4xl space-y-4 p-4 md:p-6">
      <PageHeader title={title} subtitle={subtitle} />
      <nav
        aria-label="Câu lạc bộ"
        className="flex flex-wrap gap-3 text-sm text-primary underline"
      >
        <Link to="/players">Tìm bạn chơi</Link>
        <Link to="/group-expenses">Chia chi phí</Link>
        <Link to={staff ? "/admin/club/equipment" : "/equipment"}>Dụng cụ</Link>
        <Link to={staff ? "/admin/club/coaches" : "/coaches"}>
          Huấn luyện viên
        </Link>
        <Link to={staff ? "/admin/club/tournaments" : "/tournaments"}>
          Giải đấu nhỏ
        </Link>
        <Link to="/calendar-sync">Lịch tự đồng bộ</Link>
      </nav>
      {children}
    </div>
  );
}
export function QueryStatus({
  query,
  empty,
}: {
  query: { isPending: boolean; error: Error | null; data?: unknown };
  empty?: boolean;
}) {
  return (
    <>
      {query.isPending && <p role="status">Đang tải…</p>}
      {query.error && <Callout tone="error">{query.error.message}</Callout>}
      {!query.isPending && !query.error && empty && <p>Chưa có dữ liệu.</p>}
    </>
  );
}
export function Field({
  label,
  name,
  type = "text",
  value,
  min,
  max,
  required = true,
}: {
  label: string;
  name: string;
  type?: string;
  value?: string | number;
  min?: number;
  max?: number;
  required?: boolean;
}) {
  return (
    <label className="block space-y-1">
      <span className="text-sm font-medium">{label}</span>
      <input
        className="w-full min-w-0 rounded-lg border border-line bg-surface p-3"
        name={name}
        type={type}
        defaultValue={value}
        min={min}
        max={max}
        step={type === "number" ? 1 : undefined}
        maxLength={type === "text" ? 1000 : undefined}
        required={required}
      />
    </label>
  );
}
export function Form({
  children,
  submit,
  pending,
  label,
}: {
  children: ReactNode;
  submit: (data: FormData) => void;
  pending: boolean;
  label: string;
}) {
  return (
    <form
      className="space-y-3"
      onSubmit={(e: FormEvent<HTMLFormElement>) => {
        e.preventDefault();
        submit(new FormData(e.currentTarget));
      }}
    >
      {children}
      <Button disabled={pending}>{label}</Button>
    </form>
  );
}
export const statusText: Record<string, string> = {
  Expired: "Hết hạn giữ hàng",
  Lost: "Đã lập biên bản mất",
  Damaged: "Đã lập biên bản hư",
  Reserved: "Đã giữ chỗ",
  Issued: "Đang cho thuê",
  Sold: "Đã giao cầu",
  Returned: "Đã trả",
  Cancelled: "Đã hủy",
  Completed: "Hoàn thành",
  Requested: "Chờ duyệt",
  Accepted: "Đã duyệt",
  Rejected: "Từ chối",
  Left: "Đã rút",
  Registration: "Đang đăng ký",
  Scheduled: "Đã chia bảng",
};
