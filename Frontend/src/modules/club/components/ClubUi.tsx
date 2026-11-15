import { useState, type FormEvent, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { NavLink, useLocation } from "react-router-dom";
import { Users, Wallet, Package, GraduationCap, Trophy, CalendarSync, ArrowRight, LoaderCircle, Inbox } from "lucide-react";
import FeatureHeading from "../../../components/FeatureHeading";
import "../club.css";
import { api } from "../../../services/api";
import {
  Button,
  Callout,
  SelectField,
} from "../../../components/ui";
import { useAuth } from "../../../store/auth";
import { formatClubTime } from "../utils/clubTime";
export {
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
export const features = [
  { path: "/players", label: "Tìm bạn chơi", icon: Users, hint: "Cùng trình độ, cùng giờ rảnh", steps: ["Chọn khu vực và giờ rảnh", "Gửi yêu cầu tham gia", "Chủ buổi duyệt và thống nhất sân"] },
  { path: "/group-expenses", label: "Chia chi phí", icon: Wallet, hint: "Rõ phần tiền của từng người", steps: ["Chọn đơn sân của nhóm", "Chia đều hoặc theo phút tham dự", "Ghi nhận từng khoản đã đóng"] },
  { path: "/equipment", label: "Dụng cụ", icon: Package, hint: "Vợt sẵn sàng, cầu đủ cho buổi chơi", steps: ["Chọn dụng cụ hoặc cầu", "Gửi yêu cầu và nhận tại cơ sở", "Trả vợt sau buổi chơi"] },
  { path: "/coaches", label: "Huấn luyện viên", icon: GraduationCap, hint: "Luyện tập theo lịch của bạn", steps: ["Chọn huấn luyện viên", "Xem giờ học còn chỗ", "Đặt buổi cá nhân hoặc nhóm"] },
  { path: "/tournaments", label: "Giải đấu nhỏ", icon: Trophy, hint: "Từ đăng ký đến kết quả", steps: ["Chọn giải và đăng ký đội", "Theo dõi bảng và lịch thi đấu", "Xem kết quả và xếp hạng"] },
  { path: "/calendar-sync", label: "Lịch tự đồng bộ", icon: CalendarSync, hint: "Lịch sân luôn bên bạn", steps: ["Tạo liên kết lịch riêng tư", "Đăng ký trong ứng dụng lịch", "Ứng dụng lịch tự lấy thay đổi"] },
];

export function Panel({ title, children }: { title: string; children: ReactNode }) {
  return <section className="club-panel"><header className="club-panel-heading"><h2>{title}</h2></header><div className="club-panel-body">{children}</div></section>;
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
  const { pathname } = useLocation();
  const feature = features.find((f) => pathname.endsWith(f.path)) ?? features[0];
  return (
    <div className="club-shell">
      <NavLink to="/club" className="club-overview-link"><ArrowRight size={16} aria-hidden="true" />Tất cả tiện ích CLB</NavLink>
      <div className="club-hero">
        <span className="club-eyebrow">{staff ? "Vận hành câu lạc bộ" : "Kết nối · Luyện tập · Thi đấu"}</span>
        <FeatureHeading icon={feature.icon} title={title} subtitle={subtitle} />
        <p className="club-hero-caption">{feature.hint}</p>
      </div>
      <nav
        aria-label="Câu lạc bộ"
        className="club-navigation"
      >
        {features.map(({ path, label, icon: Icon }) => <NavLink key={path} to={staff && ["/equipment", "/coaches", "/tournaments"].includes(path) ? `/admin/club${path}` : path} className={({ isActive }) => `club-nav-item${isActive ? " is-active" : ""}`}><Icon size={19} aria-hidden="true" /><span>{label}</span><ArrowRight size={14} className="club-nav-arrow" aria-hidden="true" /></NavLink>)}
      </nav>
      {!staff && <ol className="club-steps" aria-label="Các bước thực hiện">{feature.steps.map((step, index) => <li key={step}><span>{index + 1}</span>{step}</li>)}</ol>}
      <div className="club-content">{children}</div>
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
      {query.isPending && <p role="status" className="club-feedback"><LoaderCircle size={18} className="animate-spin" aria-hidden="true" />Đang tải…</p>}
      {query.error && <Callout tone="error">{query.error.message}</Callout>}
      {!query.isPending && !query.error && empty && <div className="club-empty"><Inbox size={28} aria-hidden="true" /><p>Chưa có dữ liệu.</p><span>Thử đổi bộ lọc hoặc quay lại sau để xem cập nhật.</span></div>}
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
      className="club-form"
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
