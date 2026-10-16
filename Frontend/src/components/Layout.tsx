import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { clsx } from "clsx";
import {
  Home,
  CalendarDays,
  ListChecks,
  User,
  LayoutDashboard,
  Dumbbell,
  Ticket,
  Clock,
  Users,
  ScrollText,
  LogOut,
  Feather,
} from "lucide-react";
import { useAuth, isStaff } from "../store/auth";

/**
 * Responsive shell (docs 07 §3):
 *  - mobile: bottom navigation (customer 4 items), safe-area aware.
 *  - desktop: left sidebar. Customers see the 4 customer items;
 *    Manager/Admin additionally get the admin section items.
 */
const customerItems = [
  { to: "/", label: "Trang chủ", icon: Home, end: true },
  { to: "/book", label: "Đặt sân", icon: CalendarDays },
  { to: "/bookings", label: "Đơn của tôi", icon: ListChecks },
  { to: "/account", label: "Tài khoản", icon: User },
];

const adminItems = [
  { to: "/admin", label: "Tổng quan", icon: LayoutDashboard, end: true },
  { to: "/admin/bookings", label: "Đặt chỗ", icon: ListChecks },
  { to: "/admin/courts", label: "Sân & giá", icon: Dumbbell },
  { to: "/admin/vouchers", label: "Voucher", icon: Ticket },
  { to: "/admin/slots", label: "Khung giờ", icon: Clock },
  { to: "/admin/staff", label: "Nhân viên", icon: Users },
  { to: "/admin/audit", label: "Nhật ký", icon: ScrollText },
];

function navClass({ isActive }: { isActive: boolean }) {
  return clsx(
    "group relative flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors cursor-pointer",
    isActive
      ? "bg-primary/10 text-primary"
      : "text-muted hover:bg-bg hover:text-fg",
  );
}

function mobileClass({ isActive }: { isActive: boolean }) {
  return clsx(
    "flex min-w-0 flex-1 flex-col items-center gap-1 px-1 py-2 text-[10px] font-medium transition-colors cursor-pointer",
    isActive ? "text-primary" : "text-muted",
  );
}

export default function Layout() {
  const user = useAuth((s) => s.user);
  const logout = useAuth((s) => s.logout);
  const nav = useNavigate();
  const staff = isStaff(user);

  const mobileItems = customerItems; // bottom nav stays 4 customer items

  function doLogout() {
    logout();
    nav("/auth");
  }

  return (
    <div className="flex min-h-dvh">
      {/* Desktop sidebar */}
      <aside className="hidden w-64 shrink-0 flex-col border-r border-border bg-card md:flex">
        <div className="flex items-center gap-2.5 px-5 pt-5 pb-4">
          <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary text-white shadow-sm shadow-primary/30">
            <Feather className="h-5 w-5" />
          </span>
          <div>
            <p className="text-sm leading-tight font-bold">Cầu Long</p>
            <p className="text-[11px] leading-tight text-muted">Booking</p>
          </div>
        </div>

        <div className="px-5">
          {staff && (
            <>
              <p className="mb-2 mt-2 px-1 text-[11px] font-semibold tracking-wider text-muted uppercase">
                Quản trị
              </p>
              <nav className="flex flex-col gap-1" aria-label="Điều hướng quản trị">
                {adminItems.map((it) => (
                  <NavLink key={it.to} to={it.to} end={it.end} className={navClass}>
                    <it.icon className="h-4 w-4 shrink-0" />
                    <span className="truncate">{it.label}</span>
                  </NavLink>
                ))}
              </nav>
            </>
          )}
          <p className="mb-2 mt-4 px-1 text-[11px] font-semibold tracking-wider text-muted uppercase">
            {staff ? "Khách hàng" : "Điều hướng"}
          </p>
          <nav className="flex flex-col gap-1" aria-label="Điều hướng chính">
            {customerItems.map((it) => (
              <NavLink key={it.to} to={it.to} end={it.end} className={navClass}>
                <it.icon className="h-4 w-4 shrink-0" />
                <span className="truncate">{it.label}</span>
              </NavLink>
            ))}
          </nav>
        </div>

        {user && (
          <div className="mt-auto px-4 pt-4">
            <div className="flex items-center gap-3 rounded-xl border border-border bg-bg/60 p-3">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-bold text-primary">
                {user.fullName.charAt(0).toUpperCase()}
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-semibold">{user.fullName}</p>
                <p className="text-[11px] text-muted">{user.role}</p>
              </div>
              <button
                type="button"
                onClick={doLogout}
                aria-label="Đăng xuất"
                className="flex h-8 w-8 items-center justify-center rounded-lg text-muted transition-colors hover:bg-slot-booked-bg hover:text-slot-booked-fg"
              >
                <LogOut className="h-4 w-4" />
              </button>
            </div>
          </div>
        )}
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <main className="flex-1 px-4 py-5 pb-24 md:px-6 md:pb-6">
          <Outlet />
        </main>

        {/* Mobile bottom nav (4 items, docs 07 §3) */}
        <nav
          aria-label="Điều hướng chính"
          className="fixed inset-x-0 bottom-0 z-20 border-t border-border bg-card/95 backdrop-blur md:hidden"
          style={{ paddingBottom: "env(safe-area-inset-bottom)" }}
        >
          <div className="mx-auto flex max-w-lg items-stretch px-1">
            {mobileItems.map((it) => (
              <NavLink key={it.to} to={it.to} end={it.end} className={mobileClass}>
                <it.icon className="h-5 w-5" />
                <span className="truncate">{it.label}</span>
              </NavLink>
            ))}
          </div>
        </nav>
      </div>
    </div>
  );
}
