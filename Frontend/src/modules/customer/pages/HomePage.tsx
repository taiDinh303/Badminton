import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  MapPin,
  Dumbbell,
  Clock,
  CalendarDays,
} from "lucide-react";
import { api } from "../../../services/api";
import { formatDay, formatVnd } from "../../../utils/format";
import { Card, Button, Callout, KpiCard } from "../../../components/ui";
import { useAuth } from "../../../store/auth";

/**
 * Trang chủ (docs 07 §3): hero CTA "Đặt sân" + thống kê nhanh + danh sách
 * cơ sở. Một hành động chính trên điện thoại; không dùng emoji (docs 07).
 */
export default function HomePage() {
  const user = useAuth((s) => s.user);
  const { data: branches = [], isLoading, isError } = useQuery({
    queryKey: ["branches"],
    queryFn: api.branches.list,
  });
  const { data: courts = [] } = useQuery({
    queryKey: ["courts"],
    queryFn: () => api.courts.list(),
  });

  const active = branches.filter((b) => b.isActive);
  const activeCourts = courts.filter((c) => c.isActive);
  const minPrice = activeCourts.length
    ? Math.min(...activeCourts.map((c) => c.priceLowPeak))
    : 0;

  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      {/* Hero */}
      <Card className="overflow-hidden">
        <div className="bg-primary p-5 text-white">
          <p className="text-sm text-white/80">Xin chào,</p>
          <h1 className="mt-0.5 text-2xl font-bold">
            {user?.fullName ?? "Chào bạn"}
          </h1>
          <p className="mt-2 text-sm text-white/80">
            Chọn cơ sở, khung giờ và thanh toán 1 chạm — giữ sân ngay khi đặt.
          </p>
          <Link to="/book" className="mt-4 inline-block">
            <Button variant="accent" size="md">
              <CalendarDays className="h-4 w-4" /> Đặt sân ngay
            </Button>
          </Link>
        </div>
      </Card>

      {/* Quick stats */}
      <div className="mt-4 grid grid-cols-3 gap-3">
        <KpiCard
          icon={<MapPin className="h-4 w-4" />}
          label="Cơ sở"
          value={String(active.length)}
          tone="default"
        />
        <KpiCard
          icon={<Dumbbell className="h-4 w-4" />}
          label="Sân"
          value={String(activeCourts.length)}
          tone="default"
        />
        <KpiCard
          icon={<Clock className="h-4 w-4" />}
          label="Giá từ"
          value={minPrice ? formatVnd(minPrice) : "—"}
          sub="giờ thường"
          tone="success"
        />
      </div>

      {/* Branch list */}
      <div className="mb-2 mt-6 flex items-center gap-2">
        <h2 className="text-base font-semibold">Cơ sở</h2>
        <span className="rounded-full bg-bg px-2 py-0.5 text-[11px] font-medium text-muted tnum">
          {active.length}
        </span>
      </div>

      {isLoading && <p className="text-sm text-muted">Đang tải…</p>}
      {isError && <Callout tone="error">Không tải được danh sách cơ sở.</Callout>}

      <div className="grid gap-3 sm:grid-cols-2">
        {active.map((b) => (
          <Card key={b.id} className="p-5 transition-colors hover:border-primary">
            <p className="font-semibold">{b.name}</p>
            <p className="mt-1 flex items-start gap-1.5 text-sm text-muted">
              <MapPin className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              <span className="line-clamp-1">{b.address}</span>
            </p>
            <p className="mt-2 flex items-center gap-1 text-xs text-muted tnum">
              <Clock className="h-3.5 w-3.5" /> {b.openTime}–{b.closeTime}
              {typeof b.courtCount === "number" && (
                <span className="ml-2 inline-flex items-center gap-1">
                  <Dumbbell className="h-3.5 w-3.5" /> {b.courtCount} sân
                </span>
              )}
            </p>
            <Link
              to="/book"
              className="mt-3 inline-flex items-center gap-1 text-sm font-medium text-primary"
            >
              Chọn tại đây <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          </Card>
        ))}
        {!isLoading && active.length === 0 && (
          <p className="text-sm text-muted">Chưa có cơ sở nào.</p>
        )}
      </div>

      <p className="mt-6 text-xs text-muted">
        Hôm nay {formatDay(new Date().toISOString())} · Giờ sân theo
        múi giờ Việt Nam (UTC+7)
      </p>
    </div>
  );
}
