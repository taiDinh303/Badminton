import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { ArrowRight, CalendarDays } from "lucide-react";
import { api, formatDay } from "../lib/api";
import { Card, Button, Callout } from "../components/ui";
import { useAuth } from "../store/auth";

/**
 * Trang chủ (docs 07 §3: một hành động chính trên điện thoại).
 * CTA = "Đặt sân". Kèm danh sách cơ sở để chuyển nhanh.
 */
export default function HomePage() {
  const user = useAuth((s) => s.user);
  const { data: branches = [], isFetching, isError } = useQuery({
    queryKey: ["branches"],
    queryFn: api.branches.list,
  });

  const active = branches.filter((b) => b.isActive);

  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <p className="text-sm text-muted">Xin chào, {user?.fullName ?? "bạn"} 👋</p>
      <h1 className="mt-1 text-2xl font-bold">Đặt sân cầu lông</h1>

      <Card className="mt-4">
        <div className="flex items-center gap-3">
          <CalendarDays className="h-6 w-6 text-primary" />
          <div className="flex-1">
            <p className="font-semibold">Sẵn sàng đặt sân?</p>
            <p className="text-sm text-muted">Chọn cơ sở và khung giờ, thanh toán 1 chạm.</p>
          </div>
          <Link to="/book">
            <Button variant="accent">
              Đặt sân
              <ArrowRight className="h-4 w-4" />
            </Button>
          </Link>
        </div>
      </Card>

      <h2 className="mb-2 mt-6 text-base font-semibold">Cơ sở</h2>

      {isFetching && <p className="text-sm text-muted">Đang tải…</p>}
      {isError && <Callout tone="error">Không tải được danh sách cơ sở.</Callout>}

      <div className="grid gap-3 sm:grid-cols-2">
        {active.map((b) => (
          <Card key={b.id} className="transition-colors hover:border-primary">
            <p className="font-semibold">{b.name}</p>
            <p className="mt-0.5 text-sm text-muted">{b.address}</p>
            <p className="mt-2 text-xs text-muted">
              Giờ mở: {b.openTime}–{b.closeTime}
            </p>
            <Link to="/book" className="mt-3 inline-flex text-sm font-medium text-primary">
              Chọn tại đây →
            </Link>
          </Card>
        ))}
        {!isFetching && active.length === 0 && (
          <p className="text-sm text-muted">Chưa có cơ sở nào.</p>
        )}
      </div>

      <p className="mt-6 text-xs text-muted">
        Ngày hôm nay: {formatDay(new Date().toISOString().slice(0, 10))}
      </p>
    </div>
  );
}
