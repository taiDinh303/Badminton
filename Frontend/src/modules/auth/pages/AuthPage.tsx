import { useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Feather,
  CalendarDays,
  Zap,
  ShieldCheck,
  ArrowRight,
} from "lucide-react";
import { useAuth } from "../../../store/auth";
import { ApiError } from "../../../services/api";
import { Button, Callout } from "../../../components/ui";

/**
 * Đăng nhập / đăng ký (docs 07 §4). Layout 2 cột: brand + điểm nổi bật
 * (trên desktop), form (luôn 1 cột trên mobile). Không dùng emoji.
 */
export default function AuthPage() {
  const [mode, setMode] = useState<"login" | "register">("login");
  const [phone, setPhone] = useState("");
  const [password, setPassword] = useState("");
  const [fullName, setFullName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const { login, register } = useAuth();
  const nav = useNavigate();

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      if (mode === "login") await login(phone.trim(), password);
      else await register({ fullName: fullName.trim(), phone: phone.trim(), password });
      nav("/");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Có lỗi, thử lại sau.");
    } finally {
      setBusy(false);
    }
  }

  const inputCls =
    "h-11 w-full rounded-lg border border-border bg-bg/60 px-3 text-sm text-fg outline-none transition-colors focus:border-primary";

  return (
    <div className="flex min-h-dvh items-center justify-center bg-bg p-4">
      <div className="grid w-full max-w-4xl overflow-hidden rounded-2xl border border-border bg-card shadow-sm shadow-black/[0.04] md:grid-cols-2">
        {/* Brand panel (desktop) */}
        <div className="relative hidden bg-primary p-8 text-white md:flex md:flex-col md:justify-between">
          <div>
            <div className="flex items-center gap-2.5">
              <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/15">
                <Feather className="h-5 w-5" />
              </span>
              <div>
                <p className="text-base font-bold leading-tight">Cầu Long</p>
                <p className="text-[11px] text-white/70">Booking</p>
              </div>
            </div>
            <h2 className="mt-8 text-xl font-bold leading-snug">
              Đặt sân cầu lông,
              <br />
              giữ chỗ ngay khi đặt.
            </h2>
            <p className="mt-2 text-sm text-white/80">
              Chọn cơ sở & khung giờ trống, thanh toán 1 chạm, check-in bằng mã/QR.
            </p>
          </div>

          <ul className="mt-8 space-y-3 text-sm">
            <li className="flex items-center gap-2.5">
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-white/15">
                <CalendarDays className="h-4 w-4" />
              </span>
              Khung giờ theo sân, lọc cao điểm
            </li>
            <li className="flex items-center gap-2.5">
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-white/15">
                <Zap className="h-4 w-4" />
              </span>
              Voucher & điểm tích lũy
            </li>
            <li className="flex items-center gap-2.5">
              <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-white/15">
                <ShieldCheck className="h-4 w-4" />
              </span>
              Kiểm soát check-in tại sân
            </li>
          </ul>
        </div>

        {/* Form panel */}
        <div className="p-6 sm:p-8">
          <div className="mb-1 flex items-center gap-2 md:hidden">
            <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary text-white">
              <Feather className="h-4.5 w-4.5" />
            </span>
            <div>
              <h1 className="text-lg font-bold leading-tight">Cầu Long Booking</h1>
              <p className="text-xs text-muted">Đặt sân cầu lông nhanh chóng</p>
            </div>
          </div>

          {/* Tab toggle */}
          <div className="mt-4 grid grid-cols-2 gap-1 rounded-lg bg-bg p-1 md:mt-0">
            <button
              type="button"
              onClick={() => setMode("login")}
              className={
                "h-9 rounded-md text-sm font-semibold transition-colors " +
                (mode === "login"
                  ? "bg-card text-fg shadow-sm"
                  : "text-muted hover:text-fg")
              }
            >
              Đăng nhập
            </button>
            <button
              type="button"
              onClick={() => setMode("register")}
              className={
                "h-9 rounded-md text-sm font-semibold transition-colors " +
                (mode === "register"
                  ? "bg-card text-fg shadow-sm"
                  : "text-muted hover:text-fg")
              }
            >
              Đăng ký
            </button>
          </div>

          <h1 className="mt-5 text-xl font-bold">
            {mode === "login" ? "Chào mừng trở lại" : "Tạo tài khoản khách"}
          </h1>
          <p className="mt-1 text-sm text-muted">
            {mode === "login"
              ? "Đăng nhập bằng số điện thoại để đặt sân."
              : "Điền thông tin để bắt đầu đặt sân."}
          </p>

          <form onSubmit={submit} className="mt-5 space-y-3.5">
            {mode === "register" && (
              <label className="block">
                <span className="mb-1.5 block text-sm font-medium text-muted">Họ và tên</span>
                <input
                  className={inputCls}
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  required
                />
              </label>
            )}
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-muted">Số điện thoại</span>
              <input
                className={inputCls}
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder="09xx xxx xxx"
                inputMode="tel"
                required
              />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm font-medium text-muted">Mật khẩu</span>
              <input
                className={inputCls}
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </label>

            {error && <Callout tone="error">{error}</Callout>}

            <Button type="submit" disabled={busy} className="w-full">
              {busy
                ? "Đang xử lý…"
                : mode === "login"
                  ? "Đăng nhập"
                  : "Đăng ký"}
              {!busy && <ArrowRight className="h-4 w-4" />}
            </Button>
          </form>

          <button
            type="button"
            onClick={() => setMode(mode === "login" ? "register" : "login")}
            className="mt-4 w-full text-center text-sm text-muted transition-colors hover:text-fg"
          >
            {mode === "login"
              ? "Chưa có tài khoản? Đăng ký"
              : "Đã có tài khoản? Đăng nhập"}
          </button>
        </div>
      </div>
    </div>
  );
}
