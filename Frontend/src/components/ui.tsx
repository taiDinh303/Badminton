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
  ChevronLeft,
  ChevronRight,
  Inbox,
  Search,
} from "lucide-react";
import type {
  ButtonHTMLAttributes,
  InputHTMLAttributes,
  ReactNode,
  HTMLAttributes,
  SelectHTMLAttributes,
} from "react";
import { useEffect, useRef } from "react";

export function Card({ children, className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      className={clsx(
        "rounded-xl border border-border bg-card shadow-sm shadow-black/[0.03]",
        className,
      )}
      {...rest}
    >
      {children}
    </div>
  );
}

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
    <label className="block text-sm">
      <span className="mb-1.5 block font-medium text-muted">{label}</span>
      <input
        {...input}
        className={clsx(
          "w-full rounded-lg border bg-card px-3 py-2.5 text-base text-fg outline-none transition-colors",
          error
            ? "border-state-error text-state-error"
            : "border-border focus:border-primary",
          className,
        )}
      />
      {error ? (
        <span className="mt-1 block text-xs font-medium text-state-error">{error}</span>
      ) : hint ? (
        <span className="mt-1 block text-xs text-muted">{hint}</span>
      ) : null}
    </label>
  );
}

export function SelectField({
  label,
  className,
  children,
  ...select
}: { label: string; children: ReactNode } & SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <label className="block text-sm">
      <span className="mb-1.5 block font-medium text-muted">{label}</span>
      <select
        {...select}
        aria-label={select["aria-label"] ?? label}
        className={clsx(
          "w-full appearance-none rounded-lg border border-border bg-card px-3 py-2.5 text-base text-fg outline-none transition-colors focus:border-primary",
          className,
        )}
      >
        {children}
      </select>
    </label>
  );
}

export function Button({
  variant = "primary",
  size = "md",
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "ghost" | "danger" | "accent";
  size?: "md" | "sm" | "xs";
}) {
  return (
    <button
      {...props}
      className={clsx(
        "inline-flex items-center justify-center gap-2 rounded-lg font-semibold transition-all disabled:cursor-not-allowed disabled:opacity-40",
        size === "md" && "min-h-11 px-4 text-sm",
        size === "sm" && "h-9 px-3 text-sm",
        size === "xs" && "h-8 min-h-0 px-2.5 text-xs",
        variant === "primary" &&
          "bg-primary text-white shadow-sm hover:brightness-110 active:brightness-95",
        variant === "accent" &&
          "bg-accent text-black shadow-sm hover:brightness-105 active:brightness-95",
        variant === "ghost" &&
          "border border-border bg-card text-fg hover:border-primary/40 hover:bg-bg",
        variant === "danger" &&
          "border border-state-error/30 bg-state-error/10 text-state-error hover:bg-state-error/20",
        className,
      )}
    />
  );
}

export function Busy({ label = "Đang tải…" }: { label?: string }) {
  return (
    <div className="flex items-center justify-center gap-2 py-10 text-sm text-muted">
      <Loader className="h-4 w-4 animate-spin" />
      {label}
    </div>
  );
}

export function KpiCard({
  icon,
  label,
  value,
  sub,
  tone = "default",
}: {
  icon: ReactNode;
  label: string;
  value: string;
  sub?: string;
  tone?: "default" | "success" | "warning" | "danger";
}) {
  const iconCls = {
    default: "bg-primary/10 text-primary",
    success: "bg-slot-free-bg text-slot-free-fg",
    warning: "bg-state-warning/15 text-state-warning",
    danger: "bg-slot-booked-bg text-slot-booked-fg",
  }[tone];
  return (
    <Card className="kpi-card p-4">
      <div className="flex items-start justify-between">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wider text-muted">{label}</p>
          <p className="mt-2 text-2xl font-bold tnum leading-none">{value}</p>
          {sub && <p className="mt-1.5 text-xs text-muted">{sub}</p>}
        </div>
        <span className={clsx("flex h-9 w-9 items-center justify-center rounded-lg", iconCls)}>
          {icon}
        </span>
      </div>
    </Card>
  );
}

export function Pills<T extends string>({
  options,
  value,
  onChange,
  ariaLabel,
}: {
  options: { key: T; label: string; count?: number }[];
  value: T;
  onChange: (v: T) => void;
  ariaLabel: string;
}) {
  return (
    <div className="flex flex-wrap gap-2" role="tablist" aria-label={ariaLabel}>
      {options.map((o) => {
        const active = value === o.key;
        return (
          <button
            key={o.key}
            type="button"
            role="tab"
            aria-selected={active}
            onClick={() => onChange(o.key)}
            className={clsx(
              "inline-flex items-center gap-1.5 rounded-full px-3.5 py-1.5 text-xs font-semibold transition-all cursor-pointer",
              active
                ? "bg-primary text-white shadow-sm"
                : "border border-border bg-card text-muted hover:border-primary/40 hover:text-fg",
            )}
          >
            {o.label}
            {o.count !== undefined && (
              <span
                className={clsx(
                  "rounded-full px-1.5 py-px text-[10px] font-bold tnum",
                  active ? "bg-white/25 text-white" : "bg-bg text-muted",
                )}
              >
                {o.count}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}

export function Toolbar({
  title,
  subtitle,
  search,
  actions,
}: {
  title: string;
  subtitle?: string;
  search?: { value: string; onChange: (v: string) => void; placeholder?: string };
  actions?: ReactNode;
}) {
  return (
    <div className="page-toolbar flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
        {subtitle && <p className="mt-1 text-sm text-muted">{subtitle}</p>}
      </div>
      <div className="flex flex-wrap items-center gap-2">
        {search && (
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-muted" />
            <input
              value={search.value}
              onChange={(e) => search.onChange(e.target.value)}
              placeholder={search.placeholder ?? "Tìm kiếm…"}
              aria-label="Tìm kiếm"
              className="h-9 w-44 rounded-lg border border-border bg-card pr-3 pl-9 text-sm text-fg outline-none transition-colors focus:border-primary sm:w-56"
            />
          </div>
        )}
        {actions}
      </div>
    </div>
  );
}

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
    <div className="page-header flex items-start justify-between gap-3">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
        {subtitle && <p className="mt-1 text-sm text-muted">{subtitle}</p>}
      </div>
      {right}
    </div>
  );
}

export function StatusBadge({ status, size = "md" }: { status: string; size?: "md" | "sm" }) {
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
      cls: "bg-slot-free-bg text-slot-free-fg",
      icon: <CheckCircle2 className="h-3 w-3" />,
      label: "Đã hoàn tiền",
    },
    PartialRefund: {
      cls: "bg-slot-free-bg text-slot-free-fg",
      icon: <CheckCircle2 className="h-3 w-3" />,
      label: "Hoàn một phần",
    },
    NoShow: {
      cls: "bg-slot-booked-bg text-slot-booked-fg",
      icon: <XCircle className="h-3 w-3" />,
      label: "No-show",
    },
    Success: {
      cls: "bg-slot-free-bg text-slot-free-fg",
      icon: <CheckCircle2 className="h-3 w-3" />,
      label: "Đã trả",
    },
    Failed: {
      cls: "bg-slot-booked-bg text-slot-booked-fg",
      icon: <XCircle className="h-3 w-3" />,
      label: "Lỗi",
    },
    Active: {
      cls: "bg-slot-free-bg text-slot-free-fg",
      icon: <Check className="h-3 w-3" />,
      label: "Hoạt động",
    },
    Locked: {
      cls: "bg-slot-booked-bg text-slot-booked-fg",
      icon: <Lock className="h-3 w-3" />,
      label: "Đã khóa",
    },
    Maintenance: {
      cls: "bg-slot-maint-bg text-slot-maint-fg",
      icon: <Wrench className="h-3 w-3" />,
      label: "Bảo trì",
    },
    Closed: {
      cls: "bg-slot-booked-bg text-slot-booked-fg",
      icon: <XCircle className="h-3 w-3" />,
      label: "Đóng",
    },
    Open: {
      cls: "bg-slot-free-bg text-slot-free-fg",
      icon: <CheckCircle2 className="h-3 w-3" />,
      label: "Mở",
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
        "inline-flex items-center gap-1 rounded-full font-semibold",
        size === "md" ? "px-2.5 py-1 text-xs" : "px-2 py-0.5 text-[11px]",
        s.cls,
      )}
    >
      {s.icon}
      {s.label}
    </span>
  );
}

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
    success: { cls: "border-slot-free-bg bg-slot-free-bg text-slot-free-fg", icon: <CheckCircle2 className="h-4 w-4" /> },
    warning: { cls: "border-state-warning/30 bg-state-warning/15 text-state-warning", icon: <AlertTriangle className="h-4 w-4" /> },
    error: { cls: "border-slot-booked-bg bg-slot-booked-bg text-slot-booked-fg", icon: <XCircle className="h-4 w-4" /> },
    info: { cls: "border-border bg-bg text-muted", icon: <Clock className="h-4 w-4" /> },
  }[tone];
  return (
    <div
      className={clsx(
        "flex items-start gap-2 rounded-lg border px-3 py-2.5 text-sm",
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

export function ProgressBar({
  value,
  max,
  tone = "primary",
  label,
}: {
  value: number;
  max: number;
  tone?: "primary" | "accent" | "danger";
  label?: string;
}) {
  const pct = max > 0 ? Math.min(100, Math.round((value / max) * 100)) : 0;
  const bar = {
    primary: "bg-primary",
    accent: "bg-accent",
    danger: "bg-state-error",
  }[tone];
  return (
    <div>
      <div
        className="h-2 w-full overflow-hidden rounded-full bg-bg"
        role="progressbar"
        aria-valuenow={pct}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={label ?? "Tiến độ"}
      >
        <div className={clsx("h-full rounded-full", bar)} style={{ width: `${pct}%` }} />
      </div>
    </div>
  );
}

export function EmptyState({
  icon,
  title,
  hint,
  children,
}: {
  icon?: ReactNode;
  title: string;
  hint?: string;
  children?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 px-6 py-14 text-center">
      <span className="flex h-12 w-12 items-center justify-center rounded-full bg-bg text-muted">
        {icon ?? <Inbox className="h-5 w-5" />}
      </span>
      <p className="text-sm font-semibold text-fg">{title}</p>
      {hint && <p className="max-w-xs text-xs text-muted">{hint}</p>}
      {children}
    </div>
  );
}

export function SearchInput({
  value,
  onChange,
  placeholder = "Tìm kiếm…",
  ariaLabel,
}: {
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  ariaLabel?: string;
}) {
  return (
    <div className="relative">
      <Search className="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-muted" />
      <input
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        aria-label={ariaLabel ?? placeholder}
        className="h-10 w-full rounded-lg border border-border bg-card pr-3 pl-9 text-sm text-fg outline-none transition-colors focus:border-primary sm:w-64"
      />
    </div>
  );
}

export function Modal({
  title,
  children,
  onClose,
  footer,
  width = "max-w-lg",
}: {
  title: string;
  children: ReactNode;
  onClose: () => void;
  footer?: ReactNode;
  width?: string;
}) {
  const dialog = useRef<HTMLDivElement>(null);
  const close = useRef(onClose);
  close.current = onClose;
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    const oldOverflow = document.body.style.overflow;
    const root = dialog.current!;
    const focusables = () => Array.from(root.querySelectorAll<HTMLElement>(
      'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex="0"]',
    )).filter((element) => element.getClientRects().length > 0);
    (focusables()[0] ?? root).focus();
    function onKey(e: KeyboardEvent) {
      if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); close.current(); }
      if (e.key === "Tab") {
        const elements = focusables();
        const first = elements[0]; const last = elements[elements.length - 1];
        if (!first) { e.preventDefault(); root.focus(); }
        else if (e.shiftKey && (document.activeElement === first || !root.contains(document.activeElement))) { e.preventDefault(); last.focus(); }
        else if (!e.shiftKey && (document.activeElement === last || !root.contains(document.activeElement))) { e.preventDefault(); first.focus(); }
      }
    }
    function keepFocus(e: FocusEvent) { if (!root.contains(e.target as Node)) (focusables()[0] ?? root).focus(); }
    document.addEventListener("keydown", onKey, true);
    document.addEventListener("focusin", keepFocus);
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey, true);
      document.removeEventListener("focusin", keepFocus);
      document.body.style.overflow = oldOverflow;
      if (previous?.isConnected) previous.focus();
    };
  }, []);

  return (
    <div ref={dialog} tabIndex={-1}
      className="modal-overlay fixed inset-0 z-50 flex items-end justify-center bg-black/40 p-0 backdrop-blur-sm sm:items-center sm:p-4"
      onMouseDown={(e) => e.target === e.currentTarget && onClose()}
      role="dialog"
      aria-modal="true"
      aria-label={title}
    >
      <div
        className={clsx(
          "w-full rounded-t-2xl border border-border bg-card shadow-xl sm:rounded-2xl",
          width,
        )}
      >
        <div className="flex items-center justify-between border-b border-border px-5 py-4">
          <h2 className="text-base font-bold">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Đóng"
            className="flex h-8 w-8 items-center justify-center rounded-lg text-muted transition-colors hover:bg-bg hover:text-fg"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        <div className="max-h-[60vh] overflow-y-auto px-5 py-4">{children}</div>
        {footer && (
          <div className="flex items-center justify-end gap-2 border-t border-border bg-bg/50 px-5 py-3.5 rounded-b-2xl">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
}

export function ConfirmDialog({
  title,
  message,
  confirmLabel = "Xác nhận",
  tone = "primary",
  busy,
  onConfirm,
  onCancel,
}: {
  title: string;
  message: string;
  confirmLabel?: string;
  tone?: "primary" | "danger";
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <Modal
      title={title}
      onClose={onCancel}
      width="max-w-sm"
      footer={
        <>
          <Button variant="ghost" size="sm" onClick={onCancel}>
            Hủy
          </Button>
          <Button
            variant={tone === "danger" ? "danger" : "primary"}
            size="sm"
            disabled={busy}
            onClick={onConfirm}
          >
            {busy ? "Đang xử lý…" : confirmLabel}
          </Button>
        </>
      }
    >
      <p className="text-sm text-muted">{message}</p>
    </Modal>
  );
}

export function Pagination({
  page,
  totalPages,
  totalItems,
  onPrev,
  onNext,
}: {
  page: number;
  totalPages: number;
  totalItems?: number;
  onPrev: () => void;
  onNext: () => void;
}) {
  return (
    <div className="pagination flex items-center justify-between text-sm text-muted">
      <span className="tnum">{totalItems !== undefined ? `${totalItems} bản ghi · ` : ""}Trang {page}/{totalPages || 1}</span>
      <div className="flex gap-1.5">
        <Button
          variant="ghost"
          size="sm"
          aria-label="Trang trước"
          disabled={page <= 1}
          onClick={onPrev}
        >
          <ChevronLeft className="h-4 w-4" /> Trước
        </Button>
        <Button
          variant="ghost"
          size="sm"
          aria-label="Trang sau"
          disabled={page >= (totalPages || 1)}
          onClick={onNext}
        >
          Sau <ChevronRight className="h-4 w-4" />
        </Button>
      </div>
    </div>
  );
}

export function BarChart({
  data,
  tone = "primary",
}: {
  data: { label: string; value: number; valueLabel?: string }[];
  tone?: "primary" | "accent";
}) {
  const max = Math.max(1, ...data.map((d) => Math.abs(d.value)));
  const bar = tone === "primary" ? "bg-primary" : "bg-accent";
  return (
    <div className="space-y-2.5">
      {data.map((d, i) => (
        <div key={d.label + i}>
          <div className="mb-1 flex justify-between gap-2 text-xs">
            <span className="text-muted">{d.label}</span>
            <span className="font-semibold tnum">{d.valueLabel ?? d.value.toLocaleString("vi-VN")}</span>
          </div>
          <div className="h-2 rounded-full bg-bg">
            <div
              className={clsx("h-2 rounded-full transition-all", d.value < 0 ? "bg-state-danger" : bar)}
              style={{ width: `${(Math.abs(d.value) / max) * 100}%` }}
            />
          </div>
        </div>
      ))}
      {data.length === 0 && <p className="text-sm text-muted">Chưa có dữ liệu.</p>}
    </div>
  );
}

export function SlotCell({
  state,
  label,
  sub,
  peak,
  selected,
  onClick,
  disabled,
}: {
  state: "free" | "booked" | "maint" | "closed";
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
    closed: "bg-slot-booked-bg text-slot-booked-fg",
  };
  const icon =
    state === "free" ? <Check className="h-3 w-3" /> : state === "booked" ? <Lock className="h-3 w-3" /> : <Wrench className="h-3 w-3" />;
  const text =
    state === "free" ? "Trống" : state === "booked" ? "Đã đặt" : state === "closed" ? "Đóng" : "Bảo trì";
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      aria-pressed={selected}
      aria-label={`${label} ${text}${peak ? " giờ cao điểm" : ""}`}
      className={clsx(
        "flex h-16 min-w-[64px] flex-col items-center justify-center gap-0.5 rounded-lg border px-1 text-center transition-all",
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
