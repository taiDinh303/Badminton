import { NavLink, Outlet } from "react-router-dom";
import { clsx } from "clsx";
import { Home, CalendarDays, ListChecks, User } from "lucide-react";
import { useAuth } from "../store/auth";

/**
 * Responsive shell (docs 07 §3):
 *  - mobile: bottom navigation, max 4 items, safe-area aware, never covers CTA.
 *  - desktop: left sidebar for manager/admin flows.
 */
const items = [
  { to: "/", label: "Trang chủ", icon: Home, end: true },
  { to: "/book", label: "Đặt sân", icon: CalendarDays },
  { to: "/bookings", label: "Đơn của tôi", icon: ListChecks },
  { to: "/account", label: "Tài khoản", icon: User },
];

function itemClass(isActive: boolean) {
  return clsx(
    "flex flex-col items-center gap-1 rounded-lg px-3 py-2 text-[11px] font-medium transition-colors",
    isActive ? "text-primary" : "text-muted hover:text-fg",
  );
}

export default function Layout() {
  const user = useAuth((s) => s.user);

  return (
    <div className="flex min-h-dvh">
      {/* Desktop sidebar */}
      <aside className="hidden w-60 shrink-0 border-r border-border bg-card p-4 md:block">
        <div className="mb-6 flex items-center gap-2">
          <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-sm font-bold text-white">
            🏸
          </span>
          <span className="font-semibold">Cầu Long</span>
        </div>
        <nav className="flex flex-col gap-1" aria-label="Điều hướng chính">
          {items.map((it) => (
            <NavLink
              key={it.to}
              to={it.to}
              end={it.end}
              className={({ isActive }) =>
                clsx(
                  "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors",
                  isActive
                    ? "bg-primary/10 text-primary"
                    : "text-muted hover:bg-bg hover:text-fg",
                )
              }
            >
              <it.icon className="h-4 w-4" />
              {it.label}
            </NavLink>
          ))}
        </nav>
        {user && (
          <div className="mt-6 border-t border-border pt-4 text-xs text-muted">
            <p className="font-medium text-fg">{user.fullName}</p>
            <p>{user.role}</p>
          </div>
        )}
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <main className="flex-1 pb-24 md:pb-0">
          <Outlet />
        </main>

        {/* Mobile bottom nav */}
        <nav
          aria-label="Điều hướng chính"
          className="fixed inset-x-0 bottom-0 z-20 border-t border-border bg-card/95 backdrop-blur md:hidden"
          style={{
            paddingBottom: "env(safe-area-inset-bottom)",
          }}
        >
          <div className="mx-auto flex max-w-lg items-stretch justify-around px-2">
            {items.map((it) => (
              <NavLink
                key={it.to}
                to={it.to}
                end={it.end}
                className={({ isActive }) => itemClass(isActive)}
              >
                <it.icon className="h-5 w-5" />
                {it.label}
              </NavLink>
            ))}
          </div>
        </nav>
      </div>
    </div>
  );
}
