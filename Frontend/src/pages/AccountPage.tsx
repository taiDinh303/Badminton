import { Link, useNavigate } from "react-router-dom";
import {
  LogOut,
  Star,
  Users,
  ListChecks,
  Hourglass,
  ShieldCheck,
  ChevronRight,
  Award,
} from "lucide-react";
import { useAuth } from "../store/auth";
import { Card, Button, Callout, StatusBadge } from "../components/ui";

const ROLE_LABEL: Record<string, string> = {
  Customer: "Khách",
  Manager: "Quản lý",
  Admin: "Quản trị",
};

/**
 * Tài khoản (docs 07 §4): hồ sơ + điểm/voucher + shortcut tới các trang con.
 * Redesign: a clear profile hero, a loyalty strip, and tappable quick-links
 * instead of dead "—" tiles.
 */
export default function AccountPage() {
  const user = useAuth((s) => s.user);
  const logout = useAuth((s) => s.logout);
  const nav = useNavigate();

  const quickLinks = [
    { to: "/bookings", label: "Đơn của tôi", hint: "Mã QR, trạng thái, thanh toán & đánh giá", icon: ListChecks },
    { to: "/waitlist", label: "Danh sách chờ", hint: "Slot full – bạn đang xếp hạng FIFO", icon: Hourglass },
    { to: "/groups", label: "Nhóm chơi", hint: "Tạo / tham gia nhóm & đặt sân chung", icon: Users },
    { to: "/reviews", label: "Đánh giá sân", hint: "Xem & để lại đánh giá sau buổi tập", icon: Star },
  ];

  return (
    <div className="mx-auto max-w-2xl p-4 md:p-6">
      <h1 className="text-2xl font-bold">Tài khoản</h1>

      {/* Profile hero */}
      <Card className="mt-4 p-4 md:p-5">
        <div className="flex items-center gap-4">
          <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-primary text-xl font-bold text-white">
            {(user?.fullName ?? "?").charAt(0).toUpperCase()}
          </span>
          <div className="min-w-0">
            <p className="truncate text-lg font-bold">{user?.fullName}</p>
            <p className="tnum text-sm text-muted">{user?.phone}</p>
            <div className="mt-2 flex flex-wrap items-center gap-1.5">
              <StatusBadge status="Confirmed" size="sm" />
              <span className="inline-flex items-center gap-1 rounded-full bg-bg px-2 py-0.5 text-xs font-medium text-muted">
                {user?.role === "Customer" ? ROLE_LABEL.Customer : user?.role}
              </span>
            </div>
          </div>
        </div>
        <Callout tone="info" className="mt-4">
          Số điện thoại đã xác minh qua mã OTP. Liên hệ quản trị viên nếu cần đổi.
        </Callout>
      </Card>

      {/* Loyalty strip */}
      <div className="mt-4 grid grid-cols-2 gap-3">
        <Link to="/bookings" className="block">
          <Card className="p-4 transition-colors hover:border-primary">
            <Award className="h-5 w-5 text-accent" />
            <p className="mt-2 text-xs font-medium text-muted">Điểm tích lũy</p>
            <p className="text-sm font-semibold text-fg">
              1.000 điểm = 20.000đ
            </p>
            <p className="mt-0.5 text-[11px] text-muted">
              Cộng khi đơn hoàn thành · xem chi tiết tại đơn
            </p>
          </Card>
        </Link>
        <Link to="/book" className="block">
          <Card className="p-4 transition-colors hover:border-primary">
            <ShieldCheck className="h-5 w-5 text-primary" />
            <p className="mt-2 text-xs font-medium text-muted">Voucher</p>
            <p className="text-sm font-semibold text-fg">Nhập mã khi đặt sân</p>
            <p className="mt-0.5 text-[11px] text-muted">Giảm theo % hoặc mức cố định</p>
          </Card>
        </Link>
      </div>

      {/* Quick links */}
      <div className="mt-4">
        <p className="mb-2 px-1 text-[11px] font-semibold uppercase tracking-wider text-muted">
          Tiện ích
        </p>
        <div className="space-y-2">
          {quickLinks.map((l) => (
            <Link key={l.to} to={l.to} className="block">
              <Card className="flex items-center gap-3 p-3.5 transition-colors hover:border-primary">
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
                  <l.icon className="h-4.5 w-4.5" />
                </span>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-semibold">{l.label}</p>
                  <p className="truncate text-xs text-muted">{l.hint}</p>
                </div>
                <ChevronRight className="h-4 w-4 shrink-0 text-muted" />
              </Card>
            </Link>
          ))}
        </div>
      </div>

      <div className="mt-6">
        <Button
          variant="danger"
          className="w-full"
          onClick={() => {
            logout();
            nav("/auth");
          }}
        >
          <LogOut className="h-4 w-4" />
          Đăng xuất
        </Button>
      </div>
    </div>
  );
}
