import { Link, NavLink, Outlet, useNavigate, useLocation } from "react-router-dom";
import { clsx } from "clsx";
import { Feather, LogOut, Menu, X } from "lucide-react";
import { useState, useRef } from "react";
import AccountMenu from "./AccountMenu";
import NotificationBell from "../modules/experience/components/NotificationBell";
import { customerItems, mobileItems, adminItems, experiencePageTitles } from "../app/navigation";
import { useAuth, isStaff, isAdmin } from "../store/auth";

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
  const [menuOpen, setMenuOpen] = useState(false);
  const menuButton = useRef<HTMLButtonElement>(null);
  const location = useLocation();
  const title = experiencePageTitles[location.pathname] ?? [...adminItems, ...customerItems].find(item => item.to === location.pathname)?.label ?? "Chi tiết đặt sân";
  const user = useAuth((s) => s.user);
  const logout = useAuth((s) => s.logout);
  const nav = useNavigate();
  const staff = isStaff(user);
  const admin = isAdmin(user);
  const items = adminItems.filter((it) => !it.adminOnly || admin);

  async function doLogout() {
    await nav("/home", { replace: true, flushSync: true });
    logout();
  }

  return (
    <div className="app-shell flex min-h-dvh">
      <a href="#main-content" className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-lg focus:bg-card focus:p-3">Bỏ qua điều hướng</a>
      {/* Desktop sidebar */}
      <aside className="sticky top-0 hidden h-dvh w-64 shrink-0 flex-col overflow-y-auto border-r border-border bg-card md:flex">
        <Link to="/home" aria-label="Về trang chủ" className="flex items-center gap-2.5 px-5 pt-5 pb-4">
          <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary text-white shadow-sm shadow-primary/30">
            <Feather className="h-5 w-5" />
          </span>
          <div>
            <p className="text-sm leading-tight font-bold">Cầu Lông</p>
            <p className="text-[11px] leading-tight text-muted">Booking</p>
          </div>
        </Link>

        <div className="px-5">
          {staff && (
            <>
              <p className="mb-2 mt-2 flex items-center justify-between px-1 text-[11px] font-semibold tracking-wider text-muted uppercase">
                <span>Quản trị</span>
                <span
                  className={clsx(
                    "rounded-full px-2 py-0.5 text-[10px] font-bold normal-case tracking-normal",
                    admin ? "bg-state-error/10 text-state-error" : "bg-primary/10 text-primary",
                  )}
                >
                  {user?.role}
                </span>
              </p>
              <nav className="flex flex-col gap-1" aria-label="Điều hướng quản trị">
                {items.map((it) => (
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
        <header className="app-header sticky top-0 z-30 border-b border-border bg-card/95 px-4 py-3 backdrop-blur md:px-6">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <button ref={menuButton} type="button" aria-label="Mở menu" aria-expanded={menuOpen} aria-controls="mobile-menu"
                onClick={() => setMenuOpen(!menuOpen)} className="flex h-11 w-11 items-center justify-center rounded-lg border border-border md:hidden">
                {menuOpen ? <X className="h-5 w-5" /> : <Menu className="h-5 w-5" />}
              </button>
              <div><p className="text-sm font-semibold">{title}</p><p className="text-xs text-muted">Đặt sân cầu lông · {staff ? "Vận hành cơ sở" : "Sẵn sàng cho trận đấu"}</p></div>
            </div>
            <div className="flex shrink-0 items-center gap-2"><NotificationBell /><AccountMenu /></div>
          </div>
          {menuOpen && <nav id="mobile-menu" aria-label="Menu di động" className="mt-3 max-h-[65dvh] overflow-y-auto border-t border-border pt-3 md:hidden"
            onKeyDown={event => { if (event.key === "Escape") { setMenuOpen(false); menuButton.current?.focus(); } }}>
            {[...(staff ? items : []), ...customerItems].map(item => (
              <NavLink key={item.to} to={item.to} end={item.end} className={navClass} onClick={() => setMenuOpen(false)}>
                <item.icon className="h-4 w-4" /><span>{item.label}</span>
              </NavLink>
            ))}
            <button type="button" onClick={doLogout} className="mt-2 flex min-h-11 w-full items-center gap-3 rounded-lg px-3 text-sm text-muted"><LogOut className="h-4 w-4" />Đăng xuất</button>
          </nav>}
        </header>
        <main id="main-content" tabIndex={-1} className="app-content flex-1 px-4 py-5 pb-24 md:px-6 md:pb-6">
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
