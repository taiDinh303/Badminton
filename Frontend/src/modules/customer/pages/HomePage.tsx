import "../styles/home.css";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowRight,
  MapPin,
  Dumbbell,
  Clock,
  CalendarDays,
  ListChecks,
  Users,
  Hourglass,
} from "lucide-react";
import { api } from "../../../services/api";
import LiveClock from "../../../components/LiveClock";
import { formatVnd } from "../../../utils/format";
import { Card, Callout, KpiCard } from "../../../components/ui";
import { useAuth } from "../../../store/auth";

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
    <div className="mx-auto max-w-4xl">
      <div className="customer-welcome"><div><p>Xin chào, sẵn sàng ra sân?</p><h1>{user?.fullName ?? "Chào bạn"}</h1></div><LiveClock /></div>
      {/* Hero */}
      <Card className="overflow-hidden">
        <div className="bg-primary p-5 text-white">
          <p className="text-xs tracking-widest text-white/80">LÊN LỊCH. RA SÂN.</p>
          <h2 className="mt-2 text-2xl font-bold">Một trận hay đang chờ bạn.</h2>
          <p className="mt-2 text-sm text-white/80">
            Chọn cơ sở và giờ chơi phù hợp.
          </p>
          <Link to="/book" className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-xl bg-accent px-4 py-3 text-sm font-semibold text-fg">
            <CalendarDays className="h-4 w-4" /> Đặt sân ngay
          </Link>
        </div>
      </Card>

      <nav className="quick-actions" aria-label="Thao tác nhanh">
        <Link to="/book"><CalendarDays size={23} />Lịch sân</Link>
        <Link to="/bookings"><ListChecks size={23} />Đơn của tôi</Link>
        <Link to="/groups"><Users size={23} />Nhóm của tôi</Link>
        <Link to="/waitlist"><Hourglass size={23} />Hàng chờ</Link>
      </nav>
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
              to={`/book?branchId=${b.id}`}
              className="mt-3 inline-flex items-center gap-1 text-sm font-medium text-primary"
            >
              Chọn tại đây <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          </Card>
        ))}
        {!isLoading && !isError && active.length === 0 && (
          <p className="text-sm text-muted">Chưa có cơ sở nào.</p>
        )}
      </div>

    </div>
  );
}
