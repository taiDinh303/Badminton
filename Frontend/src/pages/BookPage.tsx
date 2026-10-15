import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { motion, AnimatePresence } from "framer-motion";
import { ChevronLeft, ChevronRight, MapPin, X, Wallet, Users } from "lucide-react";
import { api, ApiError, formatDay, formatVnd, type CourtAvailability, type Slot } from "../lib/api";
import { Card, Callout, Busy, Button, SlotCell } from "../components/ui";

function localDate(d: Date): string {
  // Naive "yyyy-MM-dd" in the user's local calendar (backend DateOnly).
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function addDays(date: string, n: number): string {
  const [y, m, d] = date.split("-").map(Number);
  const dt = new Date(y, m - 1, d);
  dt.setDate(dt.getDate() + n);
  return localDate(dt);
}

type Sel = { court: CourtAvailability; slot: Slot };

/**
 * Grid đặt sân (docs 07 §3/§4):
 *  - trên: chọn cơ sở + dải ngày (mũi tên chuyển).
 *  - giữa: grid hàng = sân, cột = giờ (cuộn ngang, snap).
 *  - chạm slot trống → drawer xác nhận (sân/giá/số người/voucher/CTA).
 *  - chú giải trạng thái; 1 CTA chính; drawer không bị che bởi bottom nav.
 */
export default function BookPage() {
  const today = localDate(new Date());
  const [date, setDate] = useState(today);
  const [branchId, setBranchId] = useState<number | null>(null);
  const [sel, setSel] = useState<Sel | null>(null);
  const [players, setPlayers] = useState(2);
  const [voucher, setVoucher] = useState("");
  const [done, setDone] = useState<string | null>(null);
  const gridRef = useRef<HTMLDivElement>(null);

  const { data: branches = [] } = useQuery({
    queryKey: ["branches"],
    queryFn: api.branches.list,
  });

  // Auto-pick first branch once loaded.
  useEffect(() => {
    if (branchId == null && branches.length > 0) setBranchId(branches[0].id);
  }, [branches, branchId]);

  const { data: courts, isFetching, refetch } = useQuery({
    queryKey: ["availability", branchId, date],
    queryFn: () => api.booking.availability(branchId!, date),
    enabled: branchId != null,
    // Poll so "slot vừa bị khách khác chiếm" tự cập nhật (docs 07 §4).
    refetchInterval: 15_000,
  });

  const create = useMutation({
    mutationFn: () =>
      api.booking.create({
        courtId: sel!.court.courtId,
        date,
        slotId: sel!.slot.slotId,
        playerCount: players,
        voucherCode: voucher.trim() || null,
      }),
    onSuccess: (b) => {
      setDone(b.bookingCode);
      setSel(null);
      refetch();
    },
  });

  const [voucherErr, setVoucherErr] = useState<string | null>(null);
  const previewVoucher = useMutation({
    mutationFn: (baseAmount: number) => api.vouchers.preview(voucher.trim(), baseAmount),
    onError: () => setVoucherErr("Voucher không áp dụng cho đơn này (không đạt điều kiện tối thiểu)."),
  });

  const price = useMemo(() => {
    if (!sel) return null;
    const unit = sel.slot.isPeak ? sel.court.pricePeak : sel.court.priceLowPeak;
    return {
      base: unit,
      total: unit * players,
      peak: sel.slot.isPeak,
    };
  }, [sel, players]);

  const openCourts = courts?.filter((c) => c.slots.some((s) => !s.isLocked)) ?? [];
  // Column union of slots for a single time axis.
  const columns = useMemo(() => {
    const map = new Map<number, Slot>();
    courts?.forEach((c) => c.slots.forEach((s) => map.set(s.slotId, s)));
    return [...map.values()];
  }, [courts]);

  function selectSlot(court: CourtAvailability, slot: Slot) {
    if (slot.isLocked) return;
    setSel({ court, slot });
  }

  // Keyboard nav on the grid (docs 07 §6: arrows move, Enter selects).
  function onKeyDown(e: React.KeyboardEvent) {
    if (!sel) return;
    const ci = columns.findIndex((c) => c.slotId === sel.slot.slotId);
    if (e.key === "ArrowRight" && ci < columns.length - 1) {
      const next = columns[ci + 1];
      if (!next.isLocked) selectSlot(sel.court, next);
      e.preventDefault();
    }
    if (e.key === "ArrowLeft" && ci > 0) {
      const prev = columns[ci - 1];
      if (!prev.isLocked) selectSlot(sel.court, prev);
      e.preventDefault();
    }
    if (e.key === "Escape") setSel(null);
  }

  return (
    <div className="mx-auto max-w-5xl p-4 md:p-6" onKeyDown={onKeyDown}>
      <h1 className="text-2xl font-bold">Đặt sân</h1>

      {/* Branch + date controls */}
      <div className="mt-4 flex flex-col gap-3">
        <label className="flex items-center gap-2 text-sm text-muted">
          <MapPin className="h-4 w-4 text-primary" />
          Cơ sở
          <select
            value={branchId ?? ""}
            onChange={(e) => setBranchId(Number(e.target.value))}
            className="rounded-lg border border-border bg-card px-3 py-2 text-base text-fg outline-none focus:border-primary"
          >
            {branches.map((b) => (
              <option key={b.id} value={b.id} disabled={!b.isActive}>
                {b.name}
              </option>
            ))}
          </select>
        </label>

        <div className="flex items-center gap-2">
          <button
            type="button"
            aria-label="Ngày trước"
            onClick={() => setDate((d) => addDays(d, -1))}
            className="flex h-9 w-9 items-center justify-center rounded-lg border border-border bg-card text-muted"
          >
            <ChevronLeft className="h-4 w-4" />
          </button>
          <span className="min-w-32 text-center text-sm font-semibold tnum">{formatDay(date)}</span>
          <button
            type="button"
            aria-label="Ngày sau"
            onClick={() => setDate((d) => addDays(d, 1))}
            className="flex h-9 w-9 items-center justify-center rounded-lg border border-border bg-card text-muted"
          >
            <ChevronRight className="h-4 w-4" />
          </button>
          <input
            type="date"
            value={date}
            min={today}
            onChange={(e) => e.target.value && setDate(e.target.value)}
            className="rounded-lg border border-border bg-card px-3 py-1.5 text-sm text-fg outline-none focus:border-primary"
          />
        </div>
      </div>

      {/* Legend (docs 07 §2: text + color + icon, never color-only) */}
      <div className="mt-4 flex flex-wrap gap-3 text-xs text-muted">
        <span className="inline-flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded bg-slot-free-bg" /> Trống
        </span>
        <span className="inline-flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded bg-slot-booked-bg" /> Đã đặt
        </span>
        <span className="inline-flex items-center gap-1">
          <span className="inline-block h-3 w-3 rounded bg-slot-maint-bg" /> Bảo trì
        </span>
        <span className="inline-flex items-center gap-1 text-accent">
          <span className="inline-block h-3 w-3 rounded border border-accent" /> Giờ cao điểm
        </span>
      </div>

      {isFetching && <Busy label="Đang tải khung giờ…" />}

      {!isFetching && openCourts.length === 0 && (
        <Callout tone="info" className="mt-4">
          Chưa có dữ liệu sân vào ngày này.
        </Callout>
      )}

      {/* Grid: rows = courts, columns = slots (horizontal scroll + snap) */}
      <div ref={gridRef} className="mt-4 -mx-4 overflow-x-auto px-4 md:mx-0 md:overflow-x-visible md:px-0">
        <div className="min-w-max md:min-w-0">
          {/* Header: court label column + slot times */}
          <div className="mb-2 flex gap-2">
            <div className="w-24 shrink-0" />
            {columns.map((s) => (
              <div key={s.slotId} className="w-16 shrink-0 text-center text-xs font-medium text-muted tnum">
                {s.startTime.slice(0, 5)}
                {s.isPeak && <span className="block text-accent">cao điểm</span>}
              </div>
            ))}
          </div>

          <div className="space-y-2">
            {openCourts.map((c) => (
              <div key={c.courtId} className="flex items-center gap-2">
                <div className="w-24 shrink-0">
                  <p className="truncate text-sm font-semibold">{c.courtName}</p>
                  <p className="text-[11px] text-muted tnum">
                    {formatVnd(c.priceLowPeak)}–{formatVnd(c.pricePeak)}
                  </p>
                </div>
                {columns.map((s) => {
                  const state = s.isLocked ? "maint" : "free";
                  const isSel = sel?.court.courtId === c.courtId && sel?.slot.slotId === s.slotId;
                  return (
                    <div key={s.slotId} className="w-16 shrink-0">
                      <SlotCell
                        state={state}
                        label={s.label}
                        peak={s.isPeak}
                        selected={isSel}
                        onClick={() => selectSlot(c, s)}
                        disabled={s.isLocked}
                      />
                    </div>
                  );
                })}
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* Confirmation drawer (docs 07 §3/§4) */}
      <AnimatePresence>
        {sel && price && (
          <motion.div
            className="fixed inset-x-0 bottom-0 z-30 mx-auto w-full max-w-2xl md:bottom-4 md:right-4 md:left-auto md:w-80"
            initial={{ y: "100%" }}
            animate={{ y: 0 }}
            exit={{ y: "100%" }}
            transition={{ duration: 0.25, ease: "easeOut" }}
          >
            <div
              className="rounded-t-2xl border border-border bg-card p-4 shadow-lg md:rounded-2xl"
              style={{ marginBottom: "env(safe-area-inset-bottom)" }}
              role="dialog"
              aria-label="Xác nhận đặt sân"
            >
              <div className="flex items-start justify-between">
                <div>
                  <p className="font-semibold">{sel.court.courtName}</p>
                  <p className="text-sm text-muted">
                    {formatDay(date)} • {sel.slot.label}
                    {sel.slot.isPeak ? " (giờ cao điểm)" : ""}
                  </p>
                </div>
                <button aria-label="Đóng" onClick={() => setSel(null)} className="text-muted hover:text-fg">
                  <X className="h-5 w-5" />
                </button>
              </div>

              <div className="mt-3 flex items-center gap-3">
                <div className="flex items-center gap-2 text-sm text-muted">
                  <Users className="h-4 w-4" />
                  <input
                    type="number"
                    min={1}
                    max={10}
                    value={players}
                    onChange={(e) => setPlayers(Math.max(1, Math.min(10, Number(e.target.value))))}
                    className="w-14 rounded-lg border border-border bg-card px-2 py-1 text-fg outline-none focus:border-primary"
                    aria-label="Số người"
                  />
                </div>
                <div className="flex flex-1 items-center gap-2 text-sm text-muted">
                  <Wallet className="h-4 w-4" />
                  <input
                    value={voucher}
                    onChange={(e) => setVoucher(e.target.value)}
                    onBlur={() => {
                      setVoucherErr(null);
                      if (voucher.trim() && price) previewVoucher.mutate(price.total);
                    }}
                    placeholder="Mã voucher (tuỳ chọn)"
                    className="flex-1 rounded-lg border border-border bg-card px-2 py-1 text-fg outline-none focus:border-primary"
                    aria-label="Voucher"
                  />
                </div>
              </div>

              <div className="mt-3 flex items-center justify-between">
                <div className="text-sm text-muted">
                  {price.peak ? "Giá cao điểm" : "Giá giờ thường"}:{" "}
                  <span className="font-semibold text-fg tnum">{formatVnd(price.base)}</span> ×{" "}
                  {players}
                </div>
                <p className="text-lg font-bold tnum">{formatVnd(price.total)}</p>
              </div>

              {voucherErr && <Callout tone="error" className="mt-2">{voucherErr}</Callout>}
              {create.isError && (
                <Callout tone="error" className="mt-2">
                  {create.error instanceof ApiError
                    ? create.error.message
                    : "Không đặt được. Thử khung giờ khác."}
                </Callout>
              )}

              <Button
                variant="accent"
                className="mt-3 w-full"
                disabled={create.isPending}
                onClick={() => create.mutate()}
              >
                {create.isPending ? "Đang đặt…" : "Xác nhận đặt sân"}
              </Button>
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Success (docs 07 §5) */}
      {done && (
        <Card className="mt-4 border-slot-free-bg">
          <Callout tone="success">
            Đặt chỗ đã xác nhận. Mã: <strong>{done}</strong>. Xem QR trong "Đơn của tôi" để vào sân.
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
