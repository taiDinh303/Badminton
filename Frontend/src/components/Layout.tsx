import { NavLink, Outlet } from "react-router-dom";
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
  BadgeCheck,
} from "lucide-react";
import { useAuth, isStaff } from "../store/auth";

/**
 * Responsive shell (docs 07 §3):
 *  - mobile: bottom navigation (customer 4 items), safe-area aware.
 *  - desktop: left sidebar. Customers see the 4 customer items;
 *    Manager/Admin additionally get the 6 admin section items
 *    (Tổng quan, Đặt chỗ, Sân/giá, Voucher, Khung giờ, Nhật ký).
 */
const customerItems = [
  { to: "/", label: "Trang chủ", icon: Home, end: true },
  { to: "/book", label: "Đặt sân", icon: CalendarDays },
  { to: "/bookings", label: "Đơn của tôi", icon: ListChecks },
  { to: "/account", label: "Tài khoản", icon: User },
];

// docs 07 §3: desktop admin has 6 sidebar items (reporting + audit).
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
    "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors cursor-pointer",
    isActive ? "bg-primary/10 text-primary" : "text-muted hover:bg-bg hover:text-fg",
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
  const staff = isStaff(user);

  const sideItems = staff ? adminItems : customerItems;
  const mobileItems = customerItems; // bottom nav stays 4 customer items

  return (
    <div className="flex min-h-dvh">
      {/* Desktop sidebar */}
      <aside className="hidden w-64 shrink-0 flex-col border-r border-border bg-card p-4 md:flex">
        <div className="mb-6 flex items-center gap-2">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary text-white">
            <BadgeCheck className="h-5 w-5" />
          </span>
          <span className="text-base font-bold">Cầu Long Booking</span>
        </div>

        <p className="mb-2 px-3 text-[11px] font-semibold uppercase tracking-wide text-muted">
          {staff ? "Quản trị" : "Khách hàng"}
        </p>
        <nav className="flex flex-col gap-1" aria-label="Điều hướng chính">
          {sideItems.map((it) => (
            <NavLink key={it.to} to={it.to} end={it.end} className={navClass}>
              <it.icon className="h-4 w-4 shrink-0" />
              <span className="truncate">{it.label}</span>
            </NavLink>
          ))}
        </nav>

        {staff && (
          <>
            <p className="mb-2 mt-4 px-3 text-[11px] font-semibold uppercase tracking-wide text-muted">
              Khách hàng
            </p>
            <nav className="flex flex-col gap-1" aria-label="Điều hướng khách">
              {customerItems.map((it) => (
                <NavLink key={it.to} to={it.to} end={it.end} className={navClass}>
                  <it.icon className="h-4 w-4 shrink-0" />
                  <span className="truncate">{it.label}</span>
                </NavLink>
              ))}
            </nav>
          </>
        )}

        {user && (
          <div className="mt-auto border-t border-border pt-4 text-xs text-muted">
            <p className="font-medium text-fg">{user.fullName}</p>
            <p>{user.role}</p>
          </div>
        )}
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <main className="flex-1 px-4 pb-24 md:px-6 md:pb-6">
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
