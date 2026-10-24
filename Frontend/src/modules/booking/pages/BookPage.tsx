import { useSearchParams } from "react-router-dom";
import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  ChevronLeft,
  ChevronRight,
  MapPin,
  CalendarDays,
  Zap,
} from "lucide-react";
import { api, type CourtAvailability, type Slot, type GroupView } from "../../../services/api";
import { formatVnd } from "../../../utils/format";
import { Card, Callout, Busy, Button, SlotCell } from "../../../components/ui";
import { useAuth } from "../../../store/auth";
import { useBookingForm, isBooked, type BookingSelection } from "../hooks/useBookingForm";
import BookingConfirmationDrawer from "../components/BookingConfirmationDrawer";

function addDays(date: string, n: number): string {
  const dt = new Date(`${date}T00:00:00Z`);
  dt.setUTCDate(dt.getUTCDate() + n);
  return dt.toISOString().slice(0, 10);
}

const WEEKDAYS = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"];

type Sel = BookingSelection;

/**
 * Grid đặt sân (docs 07 §3/§4) — redesign:
 *  - header card: cơ sở + dải ngày (mũi tên) + nhanh "Hôm nay".
 *  - grid: hàng = sân, cột = giờ; cell cho biết giá theo peak/low.
 *  - drawer xác nhận: sân/giá/người/voucher/điểm/nhóm + 1 CTA chính.
 *  - chú giải trạng thái (text + màu + icon); drawer không bị che bottom nav.
 */
export default function BookPage() {
  const [searchParams] = useSearchParams();
  const requestedBranch = Number(searchParams.get("branchId"));
  const today = new Date(Date.now() + 7 * 3600_000).toISOString().slice(0, 10);
  const [date, setDate] = useState(today);
  const [branchId, setBranchId] = useState<number | null>(null);
  const [sel, setSel] = useState<Sel | null>(null);
  const { data: branches = [] } = useQuery({
    queryKey: ["branches"],
    queryFn: api.branches.list,
  });

  const user = useAuth((s) => s.user);
  const { data: myGroups = [] } = useQuery({
    queryKey: ["groups", "mine"],
    queryFn: api.groups.mine,
    select: (gs: GroupView[]) =>
      gs.filter((g) => g.ownerId === user?.id),
  });

  useEffect(() => {
    if (branchId == null && branches.length > 0) {
      const requested = branches.find(b => b.id === requestedBranch && b.isActive);
      setBranchId(requested?.id ?? branches[0].id);
    }
  }, [branches, branchId, requestedBranch]);

  const { data: courts, isLoading, refetch } = useQuery({
    queryKey: ["availability", branchId, date],
    queryFn: () => api.booking.availability(branchId!, date),
    enabled: branchId != null,
    staleTime: 10_000,
    refetchInterval: 15_000,
  });

  const form = useBookingForm(sel, date, () => { void refetch(); }, () => setSel(null));
  const { create, joinWaitlist, setWaitlistDone, done, setDone } = form;

  const openCourts =
    courts?.filter((c) => c.slots.some((s) => !s.isLocked)) ?? [];

  function selectSlot(court: CourtAvailability, slot: Slot) {
    if (slot.isLocked && !isBooked(slot)) return;
    create.reset(); joinWaitlist.reset();
    form.setPlayers(p => Math.min(p, court.capacity ?? 10));
    setSel({ court, slot });
    setWaitlistDone(false);
  }

  // Keyboard nav moves within the currently selected court's slots (docs 07 §6).
  function onKeyDown(e: React.KeyboardEvent) {
    if (!sel) return;
    const ordered = [...sel.court.slots].sort(
      (a, b) => a.startTime.localeCompare(b.startTime),
    );
    const ci = ordered.findIndex((s) => s.slotId === sel.slot.slotId);
    if (e.key === "ArrowRight" && ci < ordered.length - 1) {
      const next = ordered[ci + 1];
      if (!next.isLocked) selectSlot(sel.court, next);
      e.preventDefault();
    }
    if (e.key === "ArrowLeft" && ci > 0) {
      const prev = ordered[ci - 1];
      if (!prev.isLocked) selectSlot(sel.court, prev);
      e.preventDefault();
    }
    if (e.key === "Escape") setSel(null);
  }

  const weekday = WEEKDAYS[new Date(`${date}T00:00:00`).getDay()];

  return (
    <div className="mx-auto max-w-5xl p-4 md:p-6" onKeyDown={onKeyDown}>
      <div>
        <h1 className="text-2xl font-bold">Đặt sân</h1>
        <p className="mt-1 text-sm text-muted">
          Chọn cơ sở, ngày & khung giờ trống để đặt. Khách check-in bằng mã/QR tại sân.
        </p>
      </div>

      {/* Controls card */}
      <Card className="mt-4 p-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="flex items-center gap-2.5 text-sm">
            <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
              <MapPin className="h-4 w-4" />
            </span>
            <span className="flex-1">
              <span className="block text-[11px] font-medium text-muted">Cơ sở</span>
              <select
                value={branchId ?? ""}
                onChange={(e) => setBranchId(Number(e.target.value))}
                aria-label="Chọn cơ sở"
                className="w-full rounded-lg border border-border bg-card px-2.5 py-1.5 text-sm font-semibold text-fg outline-none focus:border-primary"
              >
                {branches.map((b) => (
                  <option key={b.id} value={b.id} disabled={!b.isActive}>
                    {b.name}
                  </option>
                ))}
              </select>
            </span>
          </label>

          <div className="sm:justify-self-end">
            <div className="flex items-center gap-2">
              <button
                type="button"
                aria-label="Ngày trước"
                onClick={() => setDate((d) => addDays(d, -1))}
                className="flex h-10 w-10 items-center justify-center rounded-lg border border-border bg-card text-muted transition-colors hover:text-fg"
              >
                <ChevronLeft className="h-4 w-4" />
              </button>
              <div className="min-w-[120px] text-center">
                <input
                  type="date"
                  value={date}
                  min={today}
                  onChange={(e) => e.target.value && setDate(e.target.value)}
                  aria-label="Chọn ngày"
                  className="mt-0.5 w-full text-center text-sm font-semibold tnum text-fg outline-none"
                />
              </div>
              <button
                type="button"
                aria-label="Ngày sau"
                onClick={() => setDate((d) => addDays(d, 1))}
                className="flex h-10 w-10 items-center justify-center rounded-lg border border-border bg-card text-muted transition-colors hover:text-fg"
              >
                <ChevronRight className="h-4 w-4" />
              </button>
            </div>
          </div>
        </div>

        <div className="mt-3 flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => setDate(today)}
            className={
              "inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-semibold transition-colors " +
              (date === today
                ? "bg-primary text-white"
                : "border border-border bg-card text-muted hover:text-fg")
            }
          >
            <CalendarDays className="h-3.5 w-3.5" /> Hôm nay
          </button>
          {/* Legend (docs 07 §2: text + color + icon) */}
          <div className="ml-auto flex flex-wrap gap-2.5 text-[11px] text-muted">
            <span className="inline-flex items-center gap-1">
              <span className="h-3 w-3 rounded bg-slot-free-bg" /> Trống
            </span>
            <span className="inline-flex items-center gap-1">
              <span className="h-3 w-3 rounded bg-slot-booked-bg" /> Đã đặt
            </span>
            <span className="inline-flex items-center gap-1">
              <span className="h-3 w-3 rounded bg-slot-maint-bg" /> Bảo trì
            </span>
            <span className="inline-flex items-center gap-1 text-accent">
              <Zap className="h-3 w-3" /> Cao điểm
            </span>
          </div>
        </div>
      </Card>

      {isLoading && <div className="mt-4"><Busy label="Đang tải khung giờ…" /></div>}

      {!isLoading && openCourts.length === 0 && (
        <Callout tone="info" className="mt-4">
          Chưa có sân trống vào ngày này.
        </Callout>
      )}

      {/* Grid — compact per-court cards, slots wrap within viewport (no h-scroll) */}
      <div className="mt-4 space-y-3">
        {(courts ?? []).map((c) => (
          <Card key={c.courtId} className="p-4">
            <div className="mb-3 flex items-center justify-between gap-2">
              <div className="min-w-0">
                <p className="truncate font-semibold">{c.courtName}</p>
                <p className="text-[11px] text-muted tnum">
                  {formatVnd(c.priceLowPeak)} – {formatVnd(c.pricePeak)}
                </p>
              </div>
              <span className="shrink-0 rounded-full bg-bg px-2.5 py-1 text-[11px] font-medium text-muted tnum">
                {c.slots.filter((s) => !s.isLocked).length}/{c.slots.length} trống
              </span>
            </div>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-5">
              {c.slots.map((s) => {
                const state = !s.isLocked ? "free" : isBooked(s) ? "booked" : s.lockReason === "Bảo trì" || s.lockReason === "San bao tri/dong" ? "maint" : "closed";
                const isSel =
                  sel?.court.courtId === c.courtId && sel?.slot.slotId === s.slotId;
                const cellPrice = s.isPeak ? c.pricePeak : c.priceLowPeak;
                return (
                  <SlotCell
                    key={s.slotId}
                    state={state}
                    label={s.startTime.slice(0, 5)}
                    sub={isBooked(s) ? "Nhấn để vào danh sách chờ" : s.isLocked ? s.lockReason : formatVnd(cellPrice)}
                    peak={s.isPeak}
                    selected={isSel}
                    onClick={() => selectSlot(c, s)}
                    disabled={s.isLocked && !isBooked(s)}
                  />
                );
              })}
            </div>
          </Card>
        ))}
      </div>

      <BookingConfirmationDrawer selection={sel} date={date} weekday={weekday} groups={myGroups} form={form} close={() => setSel(null)} />

      {/* Success */}
      {done && (
        <Card className="mt-4 border-slot-free-bg p-4">
          <Callout tone="success">
            Đặt chỗ thành công. Mã: <strong>{done}</strong>. Xem QR trong "Đơn của
            tôi" để vào sân.
          </Callout>
          <div className="mt-3">
            <Button variant="ghost" onClick={() => setDone(null)}>
              Chọn sân khác
            </Button>
          </div>
        </Card>
      )}
    </div>
  );
}
