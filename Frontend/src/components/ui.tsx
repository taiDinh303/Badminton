import { clsx } from "clsx";
import {
  Loader,
  Check,
  X,
  CalendarDays,
  Home,
  ListChecks,
  User,
  MapPin,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  Clock,
  Wrench,
  Lock,
} from "lucide-react";
import type {
  ButtonHTMLAttributes,
  InputHTMLAttributes,
  ReactNode,
  HTMLAttributes,
} from "react";

/** Card surface (docs 07 §2: bg #fff, radius 12, border #E2E8F0). */
export function Card({ children, className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={clsx("rounded-xl border border-border bg-card p-4", className)}
      {...rest}
    >
      {children}
    </div>
  );
}

/** Labeled text input with inline validation (docs 07 §6: label + inline error). */
export function Field({
  label,
  error,
  hint,
  className,
  ...input
}: {
  label: string;
  error?: string | null;
  hint?: string | null;
} & InputHTMLAttributes<HTMLInputElement>) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm text-muted">{label}</span>
      <input
        {...input}
        className={clsx(
          "w-full rounded-lg border bg-card px-3 py-2.5 text-base outline-none transition-colors",
          error
            ? "border-state-error text-state-error"
            : "border-border focus:border-primary",
          className,
        )}
      />
      {error ? (
        <span className="mt-1 block text-xs text-state-error">{error}</span>
      ) : hint ? (
        <span className="mt-1 block text-xs text-muted">{hint}</span>
      ) : null}
    </label>
  );
}

/** Primary / ghost / danger button. Min touch target 44px (docs 07 §2). */
export function Button({
  variant = "primary",
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "ghost" | "danger" | "accent";
}) {
  return (
    <button
      {...props}
      className={clsx(
        "inline-flex min-h-11 items-center justify-center gap-2 rounded-lg px-4 text-sm font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-40",
        variant === "primary" && "bg-primary text-white hover:brightness-110",
        variant === "accent" && "bg-accent text-black hover:brightness-105",
        variant === "ghost" && "border border-border text-fg hover:bg-bg",
        variant === "danger" && "bg-state-error/10 text-state-error hover:bg-state-error/20",
        className,
      )}
    />
  );
}

/** Centered spinner while async work runs. */
export function Busy({ label = "Đang tải…" }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-10 text-sm text-muted">
      <Loader className="h-4 w-4 animate-spin" />
      {label}
    </div>
  );
}

/** Status pill — always text + color (docs 07: no color-only signaling). */
export function StatusBadge({ status }: { status: string }) {
  const map: Record<string, { cls: string; icon: ReactNode; label: string }> = {
    Pending: {
      cls: "bg-state-warning/15 text-state-warning",
      icon: <Clock className="h-3 w-3" />,
      label: "Chờ thanh toán",
    },
    Confirmed: {
      cls: "bg-slot-free-bg text-slot-free-fg",
      icon: <Check className="h-3 w-3" />,
      label: "Đã xác nhận",
    },
    Completed: {
      cls: "bg-bg text-muted",
      icon: <CheckCircle2 className="h-3 w-3" />,
      label: "Đã hoàn thành",
    },
    Cancelled: {
      cls: "bg-slot-maint-bg text-slot-maint-fg",
      icon: <X className="h-3 w-3" />,
      label: "Đã hủy",
    },
    Refunded: {
      cls: "bg-state-success/10 text-state-success",
      icon: <CheckCircle2 className="h-3 w-3" />,
      label: "Đã hoàn tiền",
    },
    PartialRefund: {
      cls: "bg-state-success/10 text-state-success",
      icon: <CheckCircle2 className="h-3 w-3" />,
      label: "Hoàn tiền một phần",
    },
    NoShow: {
      cls: "bg-slot-booked-bg text-slot-booked-fg",
      icon: <XCircle className="h-3 w-3" />,
      label: "No-show",
    },
  };
  const s = map[status] ?? {
    cls: "bg-bg text-muted",
    icon: <Clock className="h-3 w-3" />,
    label: status,
  };
  return (
    <span
      className={clsx(
        "inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-medium",
        s.cls,
      )}
    >
      {s.icon}
      {s.label}
    </span>
  );
}

/** Inline success/warning/error callout (docs 07 §5 sample messages). */
export function Callout({
  tone,
  children,
  className,
}: {
  tone: "success" | "warning" | "error" | "info";
  children: ReactNode;
  className?: string;
}) {
  const map = {
    success: { cls: "bg-slot-free-bg text-slot-free-fg", icon: <CheckCircle2 className="h-4 w-4" /> },
    warning: { cls: "bg-state-warning/15 text-state-warning", icon: <AlertTriangle className="h-4 w-4" /> },
    error: { cls: "bg-slot-booked-bg text-slot-booked-fg", icon: <XCircle className="h-4 w-4" /> },
    info: { cls: "bg-bg text-muted", icon: <Clock className="h-4 w-4" /> },
  }[tone];
  return (
    <div
      className={clsx(
        "flex items-start gap-2 rounded-lg px-3 py-2.5 text-sm",
        map.cls,
        className,
      )}
      role={tone === "error" ? "alert" : "status"}
    >
      <span className="mt-0.5 shrink-0">{map.icon}</span>
      <span>{children}</span>
    </div>
  );
}

/** Page-level header: title + optional subtitle + right slot. */
export function PageHeader({
  title,
  subtitle,
  right,
}: {
  title: string;
  subtitle?: string;
  right?: ReactNode;
}) {
  return (
    <div className="flex items-start justify-between gap-3">
      <div>
        <h1 className="text-2xl font-bold">{title}</h1>
        {subtitle && <p className="mt-1 text-sm text-muted">{subtitle}</p>}
      </div>
      {right}
    </div>
  );
}

// ── Slot visual (docs 07 §2: text + color + icon, never color-only) ─

export function SlotCell({
  state,
  label,
  sub,
  peak,
  selected,
  onClick,
  disabled,
}: {
  state: "free" | "booked" | "maint";
  label: string;
  sub?: string;
  peak?: boolean;
  selected?: boolean;
  onClick?: () => void;
  disabled?: boolean;
}) {
  const base: Record<string, string> = {
    free: "bg-slot-free-bg text-slot-free-fg",
    booked: "bg-slot-booked-bg text-slot-booked-fg",
    maint: "bg-slot-maint-bg text-slot-maint-fg",
  };
  const icon =
    state === "free" ? <Check className="h-3 w-3" /> : state === "booked" ? <Lock className="h-3 w-3" /> : <Wrench className="h-3 w-3" />;
  const text =
    state === "free" ? "Trống" : state === "booked" ? "Đã đặt" : "Bảo trì";
  return (
    <button
      type="button"
      disabled={disabled || state !== "free"}
      onClick={onClick}
      aria-pressed={selected}
      aria-label={`${label} ${text}${peak ? " giờ cao điểm" : ""}`}
      className={clsx(
        "flex h-16 min-w-[64px] flex-col items-center justify-center gap-0.5 rounded-lg border px-1 text-center transition-colors",
        base[state],
        state === "free" && !disabled && "hover:ring-2 hover:ring-primary/40",
        selected && "ring-2 ring-primary",
        disabled && "cursor-not-allowed",
        peak && "border-accent",
      )}
    >
      <span className={clsx("text-xs font-semibold tnum", state !== "free" && "opacity-80")}>
        {label}
      </span>
      <span className="flex items-center gap-1 text-[10px] font-medium">
        {icon}
        {text}
      </span>
      {sub && <span className="text-[10px] opacity-70">{sub}</span>}
    </button>
  );
}

export const NAV_ICONS = {
  home: Home,
  booking: CalendarDays,
  list: ListChecks,
  user: User,
  branch: MapPin,
};
