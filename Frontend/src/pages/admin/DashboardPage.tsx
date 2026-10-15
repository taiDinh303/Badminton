import { useQuery } from "@tanstack/react-query";
import { TrendingUp, Clock, Users, Wrench, CircleDollarSign, Gauge } from "lucide-react";
import { api, formatVnd, type ReportSummary, type RevenueByDay, type OccupancyByDay } from "../../lib/api";
import { Card, PageHeader, Busy, Callout } from "../../components/ui";

function Kpi({ icon, label, value, sub }: { icon: React.ReactNode; label: string; value: string; sub?: string }) {
  return (
    <Card className="p-4">
      <div className="flex items-center gap-2 text-muted">
        {icon}
        <span className="text-xs font-medium uppercase tracking-wide">{label}</span>
      </div>
      <p className="mt-2 text-2xl font-bold tnum">{value}</p>
      {sub && <p className="text-xs text-muted">{sub}</p>}
    </Card>
  );
}

/** Horizontal bar chart (no chart lib → tiny bundle, docs 07 §6). */
function BarChart({ data, color = "bg-primary" }: { data: { label: string; value: number }[]; color?: string }) {
  const max = Math.max(1, ...data.map((d) => d.value));
  return (
    <div className="space-y-2">
      {data.map((d) => (
        <div key={d.label}>
          <div className="mb-0.5 flex justify-between text-xs text-muted">
            <span>{d.label}</span>
            <span className="tnum">{formatVnd(d.value)}</span>
          </div>
          <div className="h-2 rounded-full bg-bg">
            <div className={`h-2 rounded-full ${color}`} style={{ width: `${(d.value / max) * 100}%` }} />
          </div>
        </div>
      ))}
    </div>
  );
}

/**
 * Tổng quan (docs 07 §4): 4 KPI (doanh thu hôm nay, % lấp đầy cao điểm,
 * đặt chờ, no-show 30d) + doanh thu theo ngày + lấp đầy.
 */
export default function DashboardPage() {
  const { data: s, isFetching, isError } = useQuery({
    queryKey: ["report", "summary"],
    queryFn: () => api.admin.report.summary(),
  });

  const from = new Date();
  from.setDate(from.getDate() - 6);
  const fromStr = from.toISOString().slice(0, 10);
  const toStr = new Date().toISOString().slice(0, 10);

  const { data: revenue } = useQuery({
    queryKey: ["report", "revenue", fromStr, toStr],
    queryFn: () => api.admin.report.revenue(fromStr, toStr),
  });

  const { data: occupancy } = useQuery({
    queryKey: ["report", "occupancy", fromStr, toStr],
    queryFn: () => api.admin.report.occupancy(fromStr, toStr),
  });

  if (isFetching && !s) return <div className="p-4"><Busy label="Đang tải tổng quan…" /></div>;

  return (
    <div>
      <PageHeader
        title="Tổng quan"
        subtitle="Chỉ số vận hành hôm nay + xu hướng 7 ngày"
      />

      {isError && <Callout tone="error" className="mt-4">Không tải được dữ liệu tổng quan.</Callout>}

      {s && (
        <>
          <div className="mt-5 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <Kpi
              icon={<CircleDollarSign className="h-4 w-4" />}
              label="Doanh thu hôm nay"
              value={formatVnd(Math.max(0, s.revenueToday))}
              sub={`${s.todayBookings} đơn`}
            />
            <Kpi
              icon={<Gauge className="h-4 w-4" />}
              label="Lấp đầy cao điểm"
              value={`${s.peakFillRatePct}%`}
              sub={`${s.activeCourts} sân hoạt động`}
            />
            <Kpi
              icon={<Clock className="h-4 w-4" />}
              label="Đang chờ"
              value={String(s.pendingCount)}
              sub="đơn chưa thanh toán"
            />
            <Kpi
              icon={<Users className="h-4 w-4" />}
              label="No-show (30d)"
              value={String(s.noShowCount30d)}
              sub={`${s.activeBranches} cơ sở`}
            />
          </div>

          <div className="mt-5 grid gap-4 lg:grid-cols-2">
            <Card>
              <p className="mb-3 flex items-center gap-1 text-sm font-semibold">
                <TrendingUp className="h-4 w-4 text-primary" /> Doanh thu 7 ngày
              </p>
              {revenue && revenue.length > 0 ? (
                <BarChart
                  data={revenue.slice(-7).map((r) => ({
                    label: r.date.slice(5).replace("-", "/"),
                    value: r.revenue,
                  }))}
                />
              ) : (
                <p className="text-sm text-muted">Chưa có dữ liệu doanh thu.</p>
              )}
            </Card>

            <Card>
              <p className="mb-3 flex items-center gap-1 text-sm font-semibold">
                <Wrench className="h-4 w-4 text-primary" /> Lấp đầy 7 ngày
              </p>
              {occupancy && occupancy.length > 0 ? (
                <BarChart
                  color="bg-accent"
                  data={occupancy.slice(-7).map((o) => ({
                    label: `${o.date.slice(5).replace("-", "/")} • ${o.fillRatePct}%`,
                    value: o.bookedSlots,
                  }))}
                />
              ) : (
                <p className="text-sm text-muted">Chưa có dữ liệu lấp đầy.</p>
              )}
            </Card>
          </div>
        </>
      )}
    </div>
  );
}

// re-export for type imports elsewhere
export type { ReportSummary, RevenueByDay, OccupancyByDay };
