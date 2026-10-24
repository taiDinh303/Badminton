import { useSearchParams } from "react-router-dom";
import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { motion, AnimatePresence } from "framer-motion";
import {
  ChevronLeft,
  ChevronRight,
  MapPin,
  X,
  Users,
  Hourglass,
  CalendarDays,
  BadgePercent,
  Coins,
  Zap,
} from "lucide-react";
import { api, ApiError, type CourtAvailability, type Slot, type GroupView } from "../../../services/api";
import { formatDay, formatVnd } from "../../../utils/format";
import { Card, Callout, Busy, Button, SlotCell } from "../../../components/ui";
import { useAuth } from "../../../store/auth";

function localDate(d: Date): string {
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

const WEEKDAYS = ["CN", "T2", "T3", "T4", "T5", "T6", "T7"];

type Sel = { court: CourtAvailability; slot: Slot };

/**
 * Grid ─æß║╖t s├ón (docs 07 ┬º3/┬º4) ΓÇö redesign:
 *  - header card: c╞í sß╗ƒ + dß║úi ng├áy (m┼⌐i t├¬n) + nhanh "H├┤m nay".
 *  - grid: h├áng = s├ón, cß╗Öt = giß╗¥; cell cho biß║┐t gi├í theo peak/low.
 *  - drawer x├íc nhß║¡n: s├ón/gi├í/ng╞░ß╗¥i/voucher/─æiß╗âm/nh├│m + 1 CTA ch├¡nh.
 *  - ch├║ giß║úi trß║íng th├íi (text + m├áu + icon); drawer kh├┤ng bß╗ï che bottom nav.
 */
export default function BookPage() {
  const [searchParams] = useSearchParams();
  const requestedBranch = Number(searchParams.get("branchId"));
  const qc = useQueryClient();
  const today = localDate(new Date());
  const [date, setDate] = useState(today);
  const [branchId, setBranchId] = useState<number | null>(null);
  const [sel, setSel] = useState<Sel | null>(null);
  const [players, setPlayers] = useState(2);
  const [voucher, setVoucher] = useState("");
  const [done, setDone] = useState<string | null>(null);
  const [groupId, setGroupId] = useState<number | "">("");
  const [pointsToRedeem, setPointsToRedeem] = useState("");

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

  const create = useMutation({
    mutationFn: () =>
      api.booking.create({
        courtId: sel!.court.courtId,
        date,
        slotId: sel!.slot.slotId,
        playerCount: players,
        voucherCode: voucher.trim() || null,
        groupId: groupId === "" ? null : groupId,
        pointsToRedeem: pointsToRedeem.trim() ? Number(pointsToRedeem) : null,
      }),
    onSuccess: (b) => {
      setDone(b.bookingCode);
      setSel(null);
      setVoucher("");
      setPointsToRedeem("");
      setGroupId("");
      void qc.invalidateQueries({ queryKey: ["account"] });
      void qc.invalidateQueries({ queryKey: ["my-bookings"] });
      refetch();
    },
  });

  const [voucherErr, setVoucherErr] = useState<string | null>(null);
  const [waitlistDone, setWaitlistDone] = useState(false);
  const previewVoucher = useMutation({
    mutationFn: (baseAmount: number) => api.vouchers.preview(voucher.trim(), baseAmount),
    onError: (error) => setVoucherErr(error.message),
  });

  const joinWaitlist = useMutation({
    mutationFn: () =>
      api.waitlist.join({
        courtId: sel!.court.courtId,
        slotId: sel!.slot.slotId,
        date,
        playerCount: players,
      }),
    onSuccess: () => { setWaitlistDone(true); qc.invalidateQueries({ queryKey: ["waitlist"] }); },
  });

  const createFailed = create.isError && create.error instanceof ApiError && create.error.status === 409;
  const selectedBooked = !!sel && isBooked(sel.slot);

  const price = useMemo(() => {
    if (!sel) return null;
    const unit = sel.slot.isPeak ? sel.court.pricePeak : sel.court.priceLowPeak;
    return { base: unit, total: unit * players, peak: sel.slot.isPeak };
  }, [sel, players]);

  const openCourts =
    courts?.filter((c) => c.slots.some((s) => !s.isLocked)) ?? [];

  function selectSlot(court: CourtAvailability, slot: Slot) {
    if (slot.isLocked && !isBooked(slot)) return;
    create.reset(); joinWaitlist.reset(); setVoucherErr(null);
    setSel({ court, slot });
    setWaitlistDone(false);
  }

  // Keyboard nav moves within the currently selected court's slots (docs 07 ┬º6).
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
        <h1 className="text-2xl font-bold">─Éß║╖t s├ón</h1>
        <p className="mt-1 text-sm text-muted">
          Chß╗ìn c╞í sß╗ƒ, ng├áy & khung giß╗¥ trß╗æng ─æß╗â ─æß║╖t. Kh├ích check-in bß║▒ng m├ú/QR tß║íi s├ón.
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
              <span className="block text-[11px] font-medium text-muted">C╞í sß╗ƒ</span>
              <select
                value={branchId ?? ""}
                onChange={(e) => setBranchId(Number(e.target.value))}
                aria-label="Chß╗ìn c╞í sß╗ƒ"
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
                aria-label="Ng├áy tr╞░ß╗¢c"
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
                  aria-label="Chß╗ìn ng├áy"
                  className="mt-0.5 w-full text-center text-sm font-semibold tnum text-fg outline-none"
                />
              </div>
              <button
                type="button"
                aria-label="Ng├áy sau"
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
            <CalendarDays className="h-3.5 w-3.5" /> H├┤m nay
          </button>
          {/* Legend (docs 07 ┬º2: text + color + icon) */}
          <div className="ml-auto flex flex-wrap gap-2.5 text-[11px] text-muted">
            <span className="inline-flex items-center gap-1">
              <span className="h-3 w-3 rounded bg-slot-free-bg" /> Trß╗æng
            </span>
            <span className="inline-flex items-center gap-1">
              <span className="h-3 w-3 rounded bg-slot-booked-bg" /> ─É├ú ─æß║╖t
            </span>
            <span className="inline-flex items-center gap-1">
              <span className="h-3 w-3 rounded bg-slot-maint-bg" /> Bß║úo tr├¼
            </span>
            <span className="inline-flex items-center gap-1 text-accent">
              <Zap className="h-3 w-3" /> Cao ─æiß╗âm
            </span>
          </div>
        </div>
      </Card>

      {isLoading && <div className="mt-4"><Busy label="─Éang tß║úi khung giß╗¥ΓÇª" /></div>}

      {!isLoading && openCourts.length === 0 && (
        <Callout tone="info" className="mt-4">
          Ch╞░a c├│ s├ón trß╗æng v├áo ng├áy n├áy.
        </Callout>
      )}

      {/* Grid ΓÇö compact per-court cards, slots wrap within viewport (no h-scroll) */}
      <div className="mt-4 space-y-3">
        {(courts ?? []).map((c) => (
          <Card key={c.courtId} className="p-4">
            <div className="mb-3 flex items-center justify-between gap-2">
              <div className="min-w-0">
                <p className="truncate font-semibold">{c.courtName}</p>
                <p className="text-[11px] text-muted tnum">
                  {formatVnd(c.priceLowPeak)} ΓÇô {formatVnd(c.pricePeak)}
                </p>
              </div>
              <span className="shrink-0 rounded-full bg-bg px-2.5 py-1 text-[11px] font-medium text-muted tnum">
                {c.slots.filter((s) => !s.isLocked).length}/{c.slots.length} trß╗æng
              </span>
            </div>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 lg:grid-cols-5">
              {c.slots.map((s) => {
                const state = !s.isLocked ? "free" : isBooked(s) ? "booked" : s.lockReason === "Bß║úo tr├¼" || s.lockReason === "San bao tri/dong" ? "maint" : "closed";
                const isSel =
                  sel?.court.courtId === c.courtId && sel?.slot.slotId === s.slotId;
                const cellPrice = s.isPeak ? c.pricePeak : c.priceLowPeak;
                return (
                  <SlotCell
                    key={s.slotId}
                    state={state}
                    label={s.startTime.slice(0, 5)}
                    sub={isBooked(s) ? "Nhß║Ñn ─æß╗â v├áo danh s├ích chß╗¥" : s.isLocked ? s.lockReason : formatVnd(cellPrice)}
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

      {/* Confirmation drawer */}
      <AnimatePresence>
        {sel && price && (
          <motion.div
            className="booking-drawer fixed inset-x-0 bottom-0 z-30 mx-auto w-full max-w-2xl md:bottom-4 md:right-4 md:left-auto md:w-80"
            initial={{ y: "100%" }}
            animate={{ y: 0 }}
            exit={{ y: "100%" }}
            transition={{ duration: 0.25, ease: "easeOut" }}
          >
            <div
              className="rounded-t-2xl border border-border bg-card p-4 shadow-lg md:rounded-2xl"
              style={{ marginBottom: "env(safe-area-inset-bottom)" }}
              role="dialog"
              aria-label="X├íc nhß║¡n ─æß║╖t s├ón"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-bold">{sel.court.courtName}</p>
                  <p className="mt-0.5 text-sm text-muted tnum">
                    {weekday} {formatDay(date)} ┬╖ {sel.slot.label}
                    {sel.slot.isPeak ? " (cao ─æiß╗âm)" : ""}
                  </p>
                </div>
                <button
                  aria-label="─É├│ng"
                  onClick={() => setSel(null)}
                  className="shrink-0 rounded-lg p-1 text-muted transition-colors hover:bg-bg hover:text-fg"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>

              {/* People + voucher */}
              <div className="mt-4 grid grid-cols-2 gap-2.5">
                <label className="block">
                  <span className="mb-1 flex items-center gap-1 text-[11px] font-medium text-muted">
                    <Users className="h-3.5 w-3.5" /> Sß╗æ ng╞░ß╗¥i
                  </span>
                  <div className="flex items-center gap-1">
                    <button
                      type="button"
                      aria-label="Giß║úm"
                      onClick={() => setPlayers((p) => Math.max(1, p - 1))}
                      className="flex h-9 w-9 items-center justify-center rounded-lg border border-border bg-card text-lg font-bold text-muted"
                    >
                      ΓêÆ
                    </button>
                    <input
                      type="number"
                      min={1}
                      max={10}
                      value={players}
                      onChange={(e) =>
                        setPlayers(
                          Math.max(1, Math.min(10, Number(e.target.value) || 1)),
                        )
                      }
                      aria-label="Sß╗æ ng╞░ß╗¥i"
                      className="h-9 w-full rounded-lg border border-border bg-card px-2 text-center text-sm font-semibold tnum text-fg outline-none focus:border-primary"
                    />
                    <button
                      type="button"
                      aria-label="T─âng"
                      onClick={() => setPlayers((p) => Math.min(10, p + 1))}
                      className="flex h-9 w-9 items-center justify-center rounded-lg border border-border bg-card text-lg font-bold text-muted"
                    >
                      +
                    </button>
                  </div>
                </label>
                <label className="block">
                  <span className="mb-1 flex items-center gap-1 text-[11px] font-medium text-muted">
                    <BadgePercent className="h-3.5 w-3.5" /> Voucher
                  </span>
                  <input
                    value={voucher}
                    onChange={(e) => { setVoucher(e.target.value); setVoucherErr(null); previewVoucher.reset(); }}
                    onBlur={() => {
                      setVoucherErr(null);
                      if (voucher.trim() && price) previewVoucher.mutate(price.total);
                    }}
                    placeholder="M├ú (tuß╗│ chß╗ìn)"
                    aria-label="M├ú voucher"
                    className="h-9 w-full rounded-lg border border-border bg-card px-2.5 text-sm text-fg outline-none focus:border-primary"
                  />
                </label>
              </div>

              <p className="mt-2 text-[11px] text-muted">Mß╗ùi m├ú voucher chß╗ë d├╣ng mß╗Öt lß║ºn cho mß╗ùi t├ái khoß║ún. Bß║ín vß║½n c├│ thß╗â d├╣ng m├ú kh├íc.</p>

              {/* Group (owner-only) */}
              {myGroups.length > 0 && (
                <label className="mt-2.5 block">
                  <span className="mb-1 flex items-center gap-1 text-[11px] font-medium text-muted">
                    <Users className="h-3.5 w-3.5" /> ─Éß║╖t cho nh├│m
                  </span>
                  <select
                    value={groupId}
                    onChange={(e) =>
                      setGroupId(e.target.value === "" ? "" : Number(e.target.value))
                    }
                    aria-label="Chß╗ìn nh├│m"
                    className="h-9 w-full rounded-lg border border-border bg-card px-2.5 text-sm text-fg outline-none focus:border-primary"
                  >
                    <option value="">C├í nh├ón (mß║╖c ─æß╗ïnh)</option>
                    {myGroups.map((g) => (
                      <option key={g.id} value={g.id}>
                        {g.name} ({g.memberCount}/{g.maxMembers})
                      </option>
                    ))}
                  </select>
                </label>
              )}

              {/* Points */}
              <label className="mt-2.5 block">
                <span className="mb-1 flex items-center gap-1 text-[11px] font-medium text-muted">
                  <Coins className="h-3.5 w-3.5" /> D├╣ng ─æiß╗âm ─æß╗òi
                </span>
                <input
                  type="number"
                  min={0}
                  step={1000}
                  value={pointsToRedeem}
                  onChange={(e) => setPointsToRedeem(e.target.value)}
                  placeholder="VD 2000"
                  aria-label="Sß╗æ ─æiß╗âm ─æß╗òi"
                  className="h-9 w-full rounded-lg border border-border bg-card px-2.5 text-sm text-fg outline-none focus:border-primary"
                />
                <span className="mt-1 block text-[11px] text-muted">
                  1.000 ─æiß╗âm = 20.000─æ ┬╖ ─æß╗òi tß╗æi ─æa theo d╞░ ─æiß╗âm
                </span>
              </label>

              {/* Price summary */}
              <div className="mt-4 flex items-end justify-between rounded-lg bg-bg p-3">
                <div className="text-xs text-muted tnum">
                  {price.peak ? "Cao ─æiß╗âm" : "Giß╗¥ th╞░ß╗¥ng"} ┬╖ {formatVnd(price.base)} ├ù {players}
                </div>
                <p className="text-xl font-bold tnum text-fg">{formatVnd(price.total)}</p>
              </div>

              {voucherErr && (
                <Callout tone="error" className="mt-2">
                  {voucherErr}
                </Callout>
              )}
              {create.isError && (
                <Callout tone="error" className="mt-2">
                  {create.error instanceof ApiError
                    ? create.error.message
                    : "Kh├┤ng ─æß║╖t ─æ╞░ß╗úc. Thß╗¡ khung giß╗¥ kh├íc."}
                </Callout>
              )}

              <Button
                variant="accent"
                className="mt-3 w-full"
                disabled={create.isPending || selectedBooked}
                onClick={() => create.mutate()}
              >
                {create.isPending ? "─Éang ─æß║╖tΓÇª" : "X├íc nhß║¡n ─æß║╖t s├ón"}
              </Button>

              {(createFailed || selectedBooked) && !waitlistDone && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="mt-2 w-full"
                  disabled={joinWaitlist.isPending}
                  onClick={() => joinWaitlist.mutate()}
                >
                  <Hourglass className="h-4 w-4" />
                  {joinWaitlist.isPending ? "─Éang xß║┐p h├ángΓÇª" : "Tham gia danh s├ích chß╗¥"}
                </Button>
              )}
              {joinWaitlist.isError && <Callout tone="error" className="mt-2">{joinWaitlist.error.message}</Callout>}
              {waitlistDone && (
                <Callout tone="success" className="mt-2">
                  ─É├ú xß║┐p bß║ín v├áo danh s├ích chß╗¥ (FIFO).
                </Callout>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Success */}
      {done && (
        <Card className="mt-4 border-slot-free-bg p-4">
          <Callout tone="success">
            ─Éß║╖t chß╗ù th├ánh c├┤ng. M├ú: <strong>{done}</strong>. Xem QR trong "─É╞ín cß╗ºa
            t├┤i" ─æß╗â v├áo s├ón.
          </Callout>
          <div className="mt-3">
            <Button variant="ghost" onClick={() => setDone(null)}>
              Chß╗ìn s├ón kh├íc
            </Button>
          </div>
        </Card>
      )}
    </div>
  );
}

function isBooked(slot: Slot) { return slot.isLocked && ["─É├ú ─æß║╖t", "Da duoc dat"].includes(slot.lockReason); }
