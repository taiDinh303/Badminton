import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  TrendingUp,
  Clock,
  Users,
  Wrench,
  CircleDollarSign,
  Gauge,
  BarChart3,
  PieChart,
  Crown,
  Hourglass,
} from "lucide-react";
import { api, type ReportSummary, type RevenueByDay, type OccupancyByDay } from "../../../services/api";
import { formatVnd } from "../../../utils/format";
import { Card, KpiCard, Busy, Callout, BarChart, ProgressBar } from "../../../components/ui";
import { isAdmin, useAuth } from "../../../store/auth";

/**
 * Tổng quan (docs 07 §4): việc cần xử lý trước biểu đồ —
 * 4 KPI + doanh thu 7 ngày + lấp đầy + cảnh báo.
 */
export default function DashboardPage() {
  const isAdminUser = isAdmin(useAuth((s) => s.user));
  const { data: s, isLoading, isError, refetch } = useQuery({
    queryKey: ["report", "summary"],
    queryFn: () => api.admin.report.summary(),
  });

  const toStr = new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10);
  const from = new Date();
  from.setTime(from.getTime() + 7 * 3600_000 - 6 * 86400_000);
  const fromStr = from.toISOString().slice(0, 10);

  // Revenue/occupancy charts are [Admin] on the backend (docs 03 role matrix).
  const { data: revenue, error: revenueError } = useQuery({
    queryKey: ["report", "revenue", fromStr, toStr],
    queryFn: () => api.admin.report.revenue(fromStr, toStr),
    enabled: isAdminUser,
  });

  const { data: occupancy, error: occupancyError } = useQuery({
    queryKey: ["report", "occupancy", fromStr, toStr],
    queryFn: () => api.admin.report.occupancy(fromStr, toStr),
    enabled: isAdminUser,
  });

  if (isLoading && !s)
    return <div className="p-4"><Busy label="Đang tải tổng quan…" /></div>;

  const revenueData = (revenue ?? [])
    .slice(-7)
    .map((r) => ({
      label: r.date.slice(5).replace("-", "/"),
      value: r.revenue - r.refunds,
      valueLabel: formatVnd(r.revenue - r.refunds),
    }));

  const occ = occupancy ?? [];
  const latestOcc = occ.length > 0 ? occ[occ.length - 1] : null;

  return (
    <div>
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Tổng quan</h1>
        <p className="mt-1 text-sm text-muted">
          Chỉ số vận hành hôm nay + xu hướng 7 ngày
        </p>
      </div>

      {isError && (
        <div className="mt-4">
          <Callout tone="error">Không tải được dữ liệu tổng quan.</Callout>
        </div>
      )}

      {occ.some(day => day.historicalEstimate) && <Callout tone="info" className="mt-4">Một số ngày cũ chưa có bản ghi cấu hình lịch sử; tỷ lệ lấp đầy được ước tính theo cấu hình hiện tại.</Callout>}
      {(revenueError || occupancyError) && <Callout tone="error" className="mt-4">Không tải được biểu đồ. Vui lòng thử lại.</Callout>}
      {!!s?.attendanceToReview && <Callout tone="info" className="mt-4">Có {s.attendanceToReview} đơn đã hết giờ chưa ghi nhận khách đến. <Link className="underline" to="/admin/bookings?needsAttendance=true">Xác minh hiện diện</Link></Callout>}
      {s && (
        <>
          {/* KPI grid */}
          <div className="mt-5 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <KpiCard
              icon={<CircleDollarSign className="h-4 w-4" />}
              label="Doanh thu hôm nay"
              value={formatVnd(s.revenueToday)}
              sub={`${s.todayBookings} đơn · ${s.usersToday} khách`}
              tone="success"
            />
            <KpiCard
              icon={<Gauge className="h-4 w-4" />}
              label="Lấp đầy cao điểm"
              value={`${s.peakFillRatePct}%`}
              sub={`${s.activeCourts} sân · ${s.activeBranches} cơ sở`}
              tone="default"
            />
            <KpiCard
              icon={<Clock className="h-4 w-4" />}
              label="Đang chờ thanh toán"
              value={String(s.pendingCount)}
              sub="cần xử lý trong 10 phút"
              tone="warning"
            />
            <KpiCard
              icon={<Users className="h-4 w-4" />}
              label="No-show (30 ngày)"
              value={String(s.noShowCount30d)}
              sub="khách không đến sân"
              tone="danger"
            />
          </div>

          {/* FEAT KPI row */}
          <div className="mt-3 grid grid-cols-2 gap-3 lg:grid-cols-2">
            <KpiCard
              icon={<Crown className="h-4 w-4" />}
              label="Thân chủ Silver/Gold"
              value={String(s.activeMembers ?? 0)}
              sub="membership đang hiệu lực (điểm ×1.5–×2)"
              tone="default"
            />
            <KpiCard
              icon={<Hourglass className="h-4 w-4" />}
              label="Đang chờ slot full"
              value={String(s.waitingListCount ?? 0)}
              sub="khách trong danh sách chờ (xếp hạng FIFO)"
              tone="warning"
            />
          </div>

          {/* Charts (Admin-only: backend gates these endpoints) */}
          {isAdminUser ? (
            <div className="mt-4 grid gap-4 lg:grid-cols-2">
            <Card className="p-5">
              <div className="mb-4 flex items-center justify-between">
                <p className="flex items-center gap-1.5 text-sm font-bold">
                  <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-primary/10 text-primary">
                    <BarChart3 className="h-4 w-4" />
                  </span>
                  Doanh thu 7 ngày
                </p>
                <span className="text-xs text-muted tnum">
                  {formatVnd(revenueData.reduce((a, b) => a + b.value, 0))}
                </span>
              </div>
              <BarChart data={revenueData} />
            </Card>

            <Card className="p-5">
              <div className="mb-4 flex items-center justify-between">
                <p className="flex items-center gap-1.5 text-sm font-bold">
                  <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-accent/20 text-fg">
                    <PieChart className="h-4 w-4" />
                  </span>
                  Lấp đầy slot
                </p>
                <span className="text-xs text-muted">
                  {latestOcc ? `${latestOcc.date.slice(5).replace("-", "/")} · peak ${latestOcc.peakFillRatePct}%` : "Chưa có dữ liệu"}
                </span>
              </div>

              {latestOcc ? (
                <div className="space-y-4">
                  <div>
                    <div className="mb-1 flex justify-between text-xs">
                      <span className="text-muted">Lấp đầy chung</span>
                      <span className="font-bold tnum">{latestOcc.fillRatePct}%</span>
                    </div>
                    <ProgressBar
                      value={latestOcc.bookedSlots}
                      max={Math.max(1, latestOcc.totalSlots)}
                      label="Lấp đầy chung"
                    />
                    <p className="mt-1 text-xs text-muted tnum">
                      {latestOcc.bookedSlots}/{latestOcc.totalSlots} slot đã đặt
                    </p>
                  </div>
                  <div>
                    <div className="mb-1 flex justify-between text-xs">
                      <span className="text-muted">Lấp đầy giờ cao điểm</span>
                      <span className="font-bold tnum">{latestOcc.peakFillRatePct}%</span>
                    </div>
                    <ProgressBar
                      tone="accent"
                      value={latestOcc.peakBooked}
                      max={Math.max(1, latestOcc.peakTotal)}
                      label="Lấp đầy cao điểm"
                    />
                    <p className="mt-1 text-xs text-muted tnum">
                      {latestOcc.peakBooked}/{latestOcc.peakTotal} slot cao điểm
                    </p>
                  </div>
                  <div className="rounded-lg bg-bg p-3">
                    <div className="mb-1 flex justify-between text-xs">
                      <span className="text-muted">Xu hướng lấp đầy 7 ngày</span>
                    </div>
                    <BarChart
                      data={occ
                        .slice(-7)
                        .map((o) => ({
                          label: o.date.slice(5).replace("-", "/"),
                          value: o.fillRatePct,
                        }))}
                    />
                  </div>
                </div>
              ) : (
                <p className="py-8 text-center text-sm text-muted">
                  Chưa có dữ liệu lấp đầy.
                </p>
              )}
            </Card>
          </div>
          ) : (
            <div className="mt-4">
              <Callout tone="info">
                Biểu đồ doanh thu & lấp đầy chỉ hiển thị cho vai trò Admin.
              </Callout>
            </div>
          )}

          {/* Alert strip */}
          {(s.pendingCount > 0 || s.noShowCount30d > 0) && (
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              {s.pendingCount > 0 && (
                <Card className="border-state-warning/30 bg-state-warning/10 p-4">
                  <p className="flex items-center gap-2 text-sm font-semibold text-state-warning">
                    <Wrench className="h-4 w-4" /> {s.pendingCount} đơn đang chờ thanh toán
                  </p>
                  <p className="mt-1 text-xs text-muted">
                    Slot được giữ tối đa 10 phút. Kiểm tra mục "Đặt chỗ" để xác nhận hoặc hủy.
                  </p>
                </Card>
              )}
              {s.noShowCount30d > 0 && (
                <Card className="border-slot-booked-bg bg-slot-booked-bg p-4">
                  <p className="flex items-center gap-2 text-sm font-semibold text-slot-booked-fg">
                    <TrendingUp className="h-4 w-4" /> {s.noShowCount30d} no-show trong 30 ngày
                  </p>
                  <p className="mt-1 text-xs text-slot-booked-fg/80">
                    Khách đủ 3 lần no-show/tháng sẽ bị chặn đặt tháng sau (quy tắc docs 01 §4).
                  </p>
                </Card>
              )}
            </div>
          )}

          <button
            type="button"
            onClick={() => refetch()}
            className="mt-4 text-xs font-medium text-muted underline-offset-2 hover:text-fg hover:underline"
          >
            Làm mới dữ liệu
          </button>
        </>
      )}
    </div>
  );
}

// re-export for type imports elsewhere
export type { ReportSummary, RevenueByDay, OccupancyByDay };
