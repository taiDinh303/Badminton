import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../store/auth";
import { ApiError } from "../lib/api";
import { Button, Card, Field, Callout } from "../components/ui";

/**
 * Đăng nhập (docs 07 §4): số điện thoại + mật khẩu (dev; OTP = Supabase).
 * Trạng thái: đang gửi, lỗi, tài khoản khóa, chưa xác minh.
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

  return (
    <div className="flex min-h-dvh items-center justify-center p-4">
      <Card className="w-full max-w-sm">
        <div className="mb-1 flex items-center gap-2">
          <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary text-white">
            🏸
          </span>
          <div>
            <h1 className="text-lg font-bold">Cầu Long Booking</h1>
            <p className="text-xs text-muted">Đặt sân cầu lông nhanh chóng</p>
          </div>
        </div>
        <p className="mt-3 text-sm text-muted">
          {mode === "login"
            ? "Đăng nhập để đặt sân"
            : "Tạo tài khoản khách"}
        </p>

        <form onSubmit={submit} className="mt-5 space-y-3">
          {mode === "register" && (
            <Field
              label="Họ và tên"
              value={fullName}
              onChange={(e) => setFullName(e.target.value)}
              required
            />
          )}
          <Field
            label="Số điện thoại"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            placeholder="09xx xxx xxx"
            inputMode="tel"
            required
          />
          <Field
            label="Mật khẩu"
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />

          {error && (
            <Callout tone="error">{error}</Callout>
          )}

          <Button type="submit" disabled={busy} className="w-full">
            {busy ? "Đang xử lý…" : mode === "login" ? "Đăng nhập" : "Đăng ký"}
          </Button>
        </form>

        <button
          type="button"
          onClick={() => setMode(mode === "login" ? "register" : "login")}
          className="mt-4 w-full text-center text-sm text-muted hover:text-fg"
        >
          {mode === "login"
            ? "Chưa có tài khoản? Đăng ký"
            : "Đã có tài khoản? Đăng nhập"}
        </button>
      </Card>
    </div>
  );
}
