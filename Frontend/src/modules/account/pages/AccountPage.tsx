import { Link, useNavigate } from "react-router-dom";
import { ExperienceLinks } from "../../experience/components/ExperienceUi";
import { mediaResourceUrl } from "../../../services/http";
import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  LogOut,
  Star,
  Users,
  ListChecks,
  Hourglass,
  ShieldCheck,
  ChevronRight,
  Ticket,
  CheckCircle2,
  Coins,
} from "lucide-react";
import { useAuth } from "../../../store/auth";
import { Card, Button, Callout, StatusBadge, Busy, Field } from "../../../components/ui";
import { api } from "../../../services/api";
import { formatVnd, formatDay } from "../../../utils/format";

const ROLE_LABEL: Record<string, string> = {
  Customer: "Khách",
  Manager: "Quản lý",
  Admin: "Quản trị",
};

export default function AccountPage() {
  const user = useAuth((s) => s.user);
  const logout = useAuth((s) => s.logout);
  const nav = useNavigate();
  const qc = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState({ fullName: "", email: "", avatarUrl: "" });
  const [saved, setSaved] = useState(false);
  const [passwords, setPasswords] = useState({ currentPassword: "", newPassword: "", confirm: "" });
  const passwordChange = useMutation({
    mutationFn: () => api.auth.changePassword({ currentPassword: passwords.currentPassword, newPassword: passwords.newPassword }),
    onSuccess: () => { setPasswords({ currentPassword: "", newPassword: "", confirm: "" }); logout("Đã đổi mật khẩu. Vui lòng đăng nhập lại."); nav("/auth", { replace: true }); },
  });
  const revoke = useMutation({ mutationFn: api.auth.revokeSessions, onSuccess: () => { logout(); nav("/auth", { replace: true }); } });
  const profile = useQuery({ queryKey: ["account", "profile", user?.id], queryFn: api.account.profile, enabled: !!user });
  const update = useMutation({
    mutationFn: () => api.account.update({ fullName: form.fullName.trim(), email: form.email.trim() || null, avatarUrl: form.avatarUrl.trim() || null }),
    onSuccess: (p) => { useAuth.getState().updateProfile(p.fullName); qc.setQueryData(["account", "profile", user?.id], p); setEditing(false); setSaved(true); },
  });

  const { data: summary, isLoading, isError, refetch } = useQuery({
    queryKey: ["account", "summary"],
    queryFn: api.account.summary,
    enabled: user != null,
  });

  const pointsBalance = summary?.pointsBalance ?? 0;
  const pointsVnd = summary?.pointsRedeemableVnd ?? 0;
  const vndPer1000 = summary?.vndPer1000Points ?? 20_000;
  const ptsUnit = summary?.pointsPerVndUnit ?? 1_000;
  const usedVoucher = summary?.usedVoucher ?? null;
  const nextVoucher = summary?.nextVoucher ?? null;

  const quickLinks = [
    { to: "/bookings", label: "Đơn của tôi", hint: "Mã QR, trạng thái, thanh toán & đánh giá", icon: ListChecks },
    { to: "/waitlist", label: "Danh sách chờ", hint: "Slot full – bạn đang xếp hạng FIFO", icon: Hourglass },
    { to: "/groups", label: "Nhóm chơi", hint: "Tạo / tham gia nhóm & đặt sân chung", icon: Users },
    { to: "/reviews", label: "Đánh giá sân", hint: "Xem & để lại đánh giá sau buổi tập", icon: Star },
  ];

  function voucherValueLabel(v: NonNullable<typeof usedVoucher> | NonNullable<typeof nextVoucher>) {
    return v.type === "Percent" ? `${v.value}%` : formatVnd(v.value);
  }

  return (
    <div className="mx-auto max-w-2xl p-4 md:p-6">
      <h1 className="text-2xl font-bold">Tài khoản</h1>
        <ExperienceLinks />

      {/* Profile hero */}
      <Card className="mt-4 p-4 md:p-5">
        <div className="flex items-center gap-4">
          {profile.data?.avatarUrl ? <img src={mediaResourceUrl(profile.data.avatarUrl)} alt="Ảnh đại diện" referrerPolicy="no-referrer" className="h-14 w-14 rounded-2xl object-cover" /> : <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-primary text-xl font-bold text-white">
            {(profile.data?.fullName ?? user?.fullName ?? "?").charAt(0).toUpperCase()}
          </span>}
          <div className="min-w-0">
            <p className="truncate text-lg font-bold">{profile.data?.fullName ?? user?.fullName}</p>
            <p className="tnum text-sm text-muted">{user?.phone}</p>
            <div className="mt-2 flex flex-wrap items-center gap-1.5">
              <StatusBadge status="Confirmed" size="sm" />
              <span className="inline-flex items-center gap-1 rounded-full bg-bg px-2 py-0.5 text-xs font-medium text-muted">
                {user?.role === "Customer" ? ROLE_LABEL.Customer : user?.role}
              </span>
            </div>
          </div>
        </div>
        <p className="mt-3 text-sm text-muted">{profile.data?.email}</p>
        <Button variant="ghost" size="sm" className="mt-3" disabled={!profile.data} onClick={() => {
          const p = profile.data!; setForm({ fullName: p.fullName, email: p.email ?? "", avatarUrl: p.avatarUrl ?? "" }); setEditing(true); setSaved(false); update.reset();
        }}>Chỉnh sửa tài khoản</Button>
        {editing && <form className="mt-4 space-y-3" onSubmit={(e) => { e.preventDefault(); update.mutate(); }}>
          <Field label="Họ và tên" required maxLength={100} value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} />
          <Field label="Email" type="email" maxLength={150} value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
          <Field label="URL ảnh đại diện" type="text" maxLength={500} placeholder="https://… hoặc ảnh đã tải lên (để trống để xóa ảnh)" value={form.avatarUrl} onChange={(e) => setForm({ ...form, avatarUrl: e.target.value })} />
          <div className="flex gap-2"><Button type="submit" disabled={update.isPending || !form.fullName.trim()}>Lưu tài khoản</Button><Button type="button" variant="ghost" disabled={update.isPending} onClick={() => setEditing(false)}>Hủy</Button></div>
          {update.isError && <Callout tone="error">{update.error.message}</Callout>}
        </form>}
        {saved && <Callout tone="success" className="mt-3">Đã cập nhật tài khoản.</Callout>}
        {profile.isError && <Callout tone="error" className="mt-3">{profile.error.message}</Callout>}
        {profile.data && <div className="mt-4 rounded-lg bg-bg p-3 text-sm">
          <p className="font-semibold">Membership · {profile.data.tier === "None" ? "Khách thường" : profile.data.tier} · {profile.data.multiplier}× điểm</p>
          {profile.data.membershipExpiresAt && <p className="mt-1 text-muted">{new Date(profile.data.membershipExpiresAt) <= new Date() ? "Đã hết hạn" : "Có hiệu lực đến"} {formatDay(profile.data.membershipExpiresAt)}</p>}
        </div>}
      </Card>

      {summary?.legacyPointsConverted && <Callout tone="info" className="mt-4">Số dư điểm cũ đã được quy đổi về 1/1.000. Thông tin điểm của các đơn cũ được giữ theo thời điểm phát sinh.</Callout>}
      {/* Loyalty strip — real points balance + voucher usage (GET /account/summary) */}
      <div className="mt-4 grid grid-cols-2 gap-3">
        <Card className="p-4">
          <div className="flex items-center gap-1.5">
            <Coins className="h-4 w-4 text-accent" />
            <p className="text-xs font-medium text-muted">Điểm tích lũy</p>
          </div>
          {isLoading ? (
            <div className="mt-3"><Busy label="Đang tải điểm…" /></div>
          ) : (
            <>
              <p className="tnum mt-2 text-2xl font-bold text-fg">
                {pointsBalance.toLocaleString("vi-VN")}
                <span className="ml-1 text-xs font-medium text-muted">điểm</span>
              </p>
              <p className="tnum mt-0.5 text-[11px] text-muted">
                Giá trị đổi ≈ {formatVnd(pointsVnd)}
              </p>
              <p className="mt-0.5 text-[11px] text-muted">
                {ptsUnit.toLocaleString("vi-VN")} điểm = {formatVnd(vndPer1000)} ·
                cộng khi đơn hoàn thành
              </p>
            </>
          )}
        </Card>

        <Card className="p-4">
          <div className="flex items-center gap-1.5">
            <ShieldCheck className="h-4 w-4 text-primary" />
            <p className="text-xs font-medium text-muted">Voucher</p>
          </div>
          {isLoading ? (
            <div className="mt-3"><Busy label="Đang tải voucher…" /></div>
          ) : nextVoucher ? (
            <>
              <p className="tnum mt-2 text-sm font-semibold text-fg"><Ticket className="mr-1 inline h-3.5 w-3.5 text-primary" />{nextVoucher.code} · {voucherValueLabel(nextVoucher)}</p>
              <p className="mt-1 text-[11px] text-muted">Có {summary?.availableVouchers.length ?? 0} mã khả dụng · mỗi mã dùng một lần.</p>
              <p className="text-[11px] text-muted">Đơn tối thiểu {formatVnd(nextVoucher.minSpend)}</p>
            </>
          ) : usedVoucher ? (
            <>
              <p className="mt-2 inline-flex items-center gap-1 rounded-full bg-slot-maint-bg px-2 py-0.5 text-xs font-semibold text-muted">
                <CheckCircle2 className="h-3.5 w-3.5" /> Đã dùng
              </p>
              {usedVoucher ? (
                <p className="tnum mt-1 text-sm font-semibold text-fg">
                  {usedVoucher.code} · {voucherValueLabel(usedVoucher)}
                </p>
              ) : (
                <p className="tnum mt-1 text-sm font-semibold text-fg">Voucher</p>
              )}
              <p className="mt-0.5 text-[11px] text-muted">
                Mã này đã dùng. Bạn vẫn có thể dùng mã voucher khác.
              </p>
            </>
          ) : (
            <p className="mt-2 text-sm text-muted">Chưa có voucher khả dụng.</p>
          )}
        </Card>
      </div>

      {isError && (
        <Callout tone="error" className="mt-3">
          Không tải được điểm / voucher.
          <Button variant="ghost" size="sm" className="ml-2" onClick={() => refetch()}>
            Thử lại
          </Button>
        </Callout>
      )}

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

      <Card className="mt-6 p-5">
        <h2 className="font-semibold">Đổi mật khẩu</h2>
        <p className="mt-1 text-sm text-muted">Đổi mật khẩu sẽ đăng xuất mọi phiên trên các thiết bị.</p>
        <form className="mt-3 space-y-3" onSubmit={e => { e.preventDefault(); passwordChange.mutate(); }}>
          <Field label="Mật khẩu hiện tại" type="password" autoComplete="current-password" required value={passwords.currentPassword} onChange={e => setPasswords({ ...passwords, currentPassword: e.target.value })} />
          <Field label="Mật khẩu mới" type="password" autoComplete="new-password" required minLength={8} maxLength={72} value={passwords.newPassword} onChange={e => setPasswords({ ...passwords, newPassword: e.target.value })} />
          <Field label="Nhập lại mật khẩu mới" type="password" autoComplete="new-password" required value={passwords.confirm} onChange={e => setPasswords({ ...passwords, confirm: e.target.value })} />
          {passwords.confirm && passwords.confirm !== passwords.newPassword && <Callout tone="error">Mật khẩu nhập lại chưa khớp.</Callout>}
          {passwordChange.isError && <Callout tone="error">{passwordChange.error.message}</Callout>}
          <Button type="submit" disabled={passwordChange.isPending || !passwords.currentPassword || passwords.newPassword.length < 8 || passwords.newPassword !== passwords.confirm}>Đổi mật khẩu và đăng xuất</Button>
        </form>
      </Card>
      <Card className="mt-4 p-5">
        <h2 className="font-semibold">Phiên đăng nhập</h2>
        <p className="my-2 text-sm text-muted">Thu hồi quyền truy cập của mọi thiết bị đang đăng nhập tài khoản này.</p>
        <Button variant="danger" disabled={revoke.isPending} onClick={() => revoke.mutate()}>Đăng xuất mọi thiết bị</Button>
        {revoke.isError && <Callout tone="error" className="mt-2">{revoke.error.message}</Callout>}
      </Card>

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
