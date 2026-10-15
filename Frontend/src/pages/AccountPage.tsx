import { useNavigate } from "react-router-dom";
import { LogOut, Award, Ticket, Users } from "lucide-react";
import { useAuth } from "../store/auth";
import { Card, Button, Callout } from "../components/ui";

/**
 * Tài khoản (docs 07 §4): hồ sơ, điểm tích lũy, voucher, nhóm chơi;
 * trạng thái chưa xác minh; redeem điểm; logout.
 */
export default function AccountPage() {
  const user = useAuth((s) => s.user);
  const logout = useAuth((s) => s.logout);
  const nav = useNavigate();

  return (
    <div className="mx-auto max-w-2xl p-4 md:p-6">
      <h1 className="text-2xl font-bold">Tài khoản</h1>

      <Card className="mt-4">
        <div className="flex items-center gap-3">
          <span className="flex h-12 w-12 items-center justify-center rounded-full bg-primary/10 text-lg font-bold text-primary">
            {(user?.fullName ?? "?").charAt(0).toUpperCase()}
          </span>
          <div>
            <p className="font-semibold">{user?.fullName}</p>
            <p className="text-sm text-muted tnum">{user?.phone}</p>
            <p className="mt-1 inline-block rounded-full bg-bg px-2 py-0.5 text-xs text-muted">
              {user?.role}
            </p>
          </div>
        </div>
        <Callout tone="info" className="mt-4">
          Số điện thoại đã xác minh qua mã OTP. Liên hệ quản trị viên để đổi.
        </Callout>
      </Card>

      {/* Điểm & voucher (docs 07 §4) */}
      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        <Card className="flex items-center gap-3">
          <Award className="h-6 w-6 text-accent" />
          <div>
            <p className="text-sm text-muted">Điểm tích lũy</p>
            <p className="text-lg font-bold tnum">—</p>
            <p className="text-xs text-muted">1000đ = 20.000đ khi quy đổi</p>
          </div>
        </Card>
        <Card className="flex items-center gap-3">
          <Ticket className="h-6 w-6 text-primary" />
          <div>
            <p className="text-sm text-muted">Voucher của tôi</p>
            <p className="text-lg font-bold">0</p>
            <p className="text-xs text-muted">Xem trong đơn đặt sân</p>
          </div>
        </Card>
      </div>

      <Card className="mt-4 flex items-center gap-3">
        <Users className="h-6 w-6 text-muted" />
        <div>
          <p className="text-sm text-muted">Nhóm chơi</p>
          <p className="text-sm font-medium">Chưa tham gia nhóm nào</p>
        </div>
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
