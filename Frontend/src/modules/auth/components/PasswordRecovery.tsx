import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { api } from "../../../services/api";
import { Modal, Field, Button, Callout } from "../../../components/ui";

export function PasswordRecovery({ onClose }: { onClose: () => void }) {
  const [phone, setPhone] = useState(""); const [code, setCode] = useState(""); const [password, setPassword] = useState("");
  const reset = useMutation({ mutationFn: () => api.auth.resetPassword({ phoneNumber: phone.trim(), code: code.trim(), newPassword: password }), onSuccess: () => { setCode(""); setPassword(""); } });
  return <Modal title="Khôi phục mật khẩu" onClose={onClose}>
    <p className="mb-3 text-sm text-muted">Liên hệ quản trị sân để xác minh tài khoản và nhận mã khôi phục có hiệu lực 15 phút. Mã chỉ dùng một lần.</p>
    {reset.isSuccess ? <Callout tone="success">Đã đặt lại mật khẩu. Đóng cửa sổ và đăng nhập bằng mật khẩu mới.</Callout> : <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); reset.mutate(); }}>
      <Field label="Số điện thoại khôi phục" inputMode="tel" required value={phone} onChange={(e) => setPhone(e.target.value)} />
      <Field label="Mã khôi phục" required autoComplete="one-time-code" value={code} onChange={(e) => setCode(e.target.value)} />
      <Field label="Mật khẩu mới" type="password" autoComplete="new-password" required minLength={8} maxLength={72} value={password} onChange={(e) => setPassword(e.target.value)} />
      {reset.isError && <Callout tone="error">{reset.error.message}</Callout>}
      <Button type="submit" disabled={reset.isPending}>Đặt lại mật khẩu</Button>
    </form>}
  </Modal>;
}

export function IssueRecoveryCode({ userId, name, onClose }: { userId: number; name: string; onClose: () => void }) {
  const [password, setPassword] = useState(""); const [reason, setReason] = useState(""); const [copied, setCopied] = useState(false);
  const issue = useMutation({ mutationFn: () => api.auth.recoveryCode(userId, { currentPassword: password, reason }), onSuccess: () => setPassword("") });
  return <Modal title={`Khôi phục tài khoản · ${name}`} onClose={onClose}>
    <p className="mb-3 text-sm text-muted">Chỉ cấp mã sau khi đã xác minh người yêu cầu là chủ tài khoản. Giao mã riêng cho khách. Cấp mã mới sẽ thu hồi mã trước.</p>
    {issue.data ? <div className="space-y-3"><Field label="Mã cấp cho khách" readOnly value={issue.data.code} />
      <p className="text-sm">Hết hạn: {new Date(issue.data.expiresAt).toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })}</p>
      <Button onClick={async () => { try { await navigator.clipboard.writeText(issue.data!.code); setCopied(true); } catch { setCopied(false); } }}>{copied ? "Đã sao chép" : "Sao chép mã"}</Button></div>
    : <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); issue.mutate(); }}>
      <Field label="Kết quả xác minh và lý do" required minLength={5} maxLength={500} value={reason} onChange={(e) => setReason(e.target.value)} />
      <Field label="Mật khẩu Admin" type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
      {issue.isError && <Callout tone="error">{issue.error.message}</Callout>}
      <Button type="submit" disabled={issue.isPending}>Cấp mã một lần</Button>
    </form>}
  </Modal>;
}
