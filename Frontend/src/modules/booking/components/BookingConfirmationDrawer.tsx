import { motion, AnimatePresence } from "framer-motion";
import { useEffect, useRef } from "react";
import { X, Users, Hourglass, BadgePercent, Coins } from "lucide-react";
import { ApiError, type GroupView } from "../../../services/api";
import { formatDay, formatVnd } from "../../../utils/format";
import { Button, Callout } from "../../../components/ui";
import type { BookingForm, BookingSelection } from "../hooks/useBookingForm";

interface Props { selection: BookingSelection | null; date: string; weekday: string; groups: GroupView[]; form: BookingForm; close: () => void }
export default function BookingConfirmationDrawer({ selection: sel, date, weekday, groups: myGroups, form, close }: Props) {
  const dialog = useRef<HTMLDivElement>(null);
  const open = sel != null;
  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement as HTMLElement | null;
    dialog.current?.focus();
    return () => { if (previous?.isConnected) previous.focus(); };
  }, [open]);
  const { players, setPlayers, voucher, setVoucher, groupId, setGroupId, pointsToRedeem, setPointsToRedeem,
    create, joinWaitlist, quote, validQuote, capacity, waitlistDone, createFailed, selectedBooked, price } = form;
  return (<>
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
              ref={dialog}
              tabIndex={-1}
              onKeyDown={event => { if (event.key === "Escape") { event.stopPropagation(); close(); } }}
              className="rounded-t-2xl border border-border bg-card p-4 shadow-lg md:rounded-2xl"
              style={{ marginBottom: "env(safe-area-inset-bottom)" }}
              role="dialog"
              aria-label="Xác nhận đặt sân"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-bold">{sel.court.courtName}</p>
                  <p className="mt-0.5 text-sm text-muted tnum">
                    {weekday} {formatDay(date)} · {sel.slot.label}
                    {sel.slot.isPeak ? " (cao điểm)" : ""}
                  </p>
                </div>
                <button
                  aria-label="Đóng"
                  onClick={() => close()}
                  className="shrink-0 rounded-lg p-1 text-muted transition-colors hover:bg-bg hover:text-fg"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>

              {/* People + voucher */}
              <div className="mt-4 grid grid-cols-2 gap-2.5">
                <label className="block">
                  <span className="mb-1 flex items-center gap-1 text-[11px] font-medium text-muted">
                    <Users className="h-3.5 w-3.5" /> Số người
                  </span>
                  <div className="flex items-center gap-1">
                    <button
                      type="button"
                      aria-label="Giảm"
                      onClick={() => setPlayers((p) => Math.max(1, p - 1))}
                      className="flex h-9 w-9 items-center justify-center rounded-lg border border-border bg-card text-lg font-bold text-muted"
                    >
                      −
                    </button>
                    <input
                      type="number"
                      min={1}
                      max={capacity}
                      value={players}
                      onChange={(e) =>
                        setPlayers(
                          Math.max(1, Math.min(capacity, Number(e.target.value) || 1)),
                        )
                      }
                      aria-label="Số người"
                      className="h-9 w-full rounded-lg border border-border bg-card px-2 text-center text-sm font-semibold tnum text-fg outline-none focus:border-primary"
                    />
                    <button
                      type="button"
                      aria-label="Tăng"
                      onClick={() => setPlayers((p) => Math.min(capacity, p + 1))}
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
                    onChange={(e) => setVoucher(e.target.value)}
                    placeholder="Mã (tuỳ chọn)"
                    aria-label="Mã voucher"
                    className="h-9 w-full rounded-lg border border-border bg-card px-2.5 text-sm text-fg outline-none focus:border-primary"
                  />
                </label>
              </div>

              <p className="mt-2 text-[11px] text-muted">Mỗi mã dùng một lần / tài khoản.</p>

              {/* Group (owner-only) */}
              {myGroups.length > 0 && (
                <label className="mt-2.5 block">
                  <span className="mb-1 flex items-center gap-1 text-[11px] font-medium text-muted">
                    <Users className="h-3.5 w-3.5" /> Đặt cho nhóm
                  </span>
                  <select
                    value={groupId}
                    onChange={(e) =>
                      setGroupId(e.target.value === "" ? "" : Number(e.target.value))
                    }
                    aria-label="Chọn nhóm"
                    className="h-9 w-full rounded-lg border border-border bg-card px-2.5 text-sm text-fg outline-none focus:border-primary"
                  >
                    <option value="">Cá nhân (mặc định)</option>
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
                  <Coins className="h-3.5 w-3.5" /> Dùng điểm đổi
                </span>
                <input
                  type="number"
                  min={0}
                  step={1000}
                  value={pointsToRedeem}
                  onChange={(e) => setPointsToRedeem(e.target.value)}
                  placeholder="VD 2000"
                  aria-label="Số điểm đổi"
                  className="h-9 w-full rounded-lg border border-border bg-card px-2.5 text-sm text-fg outline-none focus:border-primary"
                />
                <span className="mt-1 block text-[11px] text-muted">
                  1.000 điểm = 20.000đ · đổi tối đa theo dư điểm
                </span>
              </label>

              {/* Price summary */}
              <div className="mt-4 flex items-end justify-between rounded-lg bg-bg p-3">
                <div className="text-xs text-muted tnum">
                  {price.peak ? "Cao điểm" : "Giờ thường"} · {formatVnd(price.base)} / sân / slot
                </div>
                <p className="text-xl font-bold tnum text-fg">{quote.isError ? "—" : price.total === null || quote.isFetching ? "Đang tính…" : formatVnd(price.total)}</p>
              </div>

              <p className="mt-2 text-xs text-muted">Sức chứa tối đa {capacity} người. Số người không làm thay đổi giá sân.</p>
              <p className="mt-2 text-xs text-muted">Mang mã đặt/QR đến sân để nhân viên check-in, từ 30 phút trước giờ bắt đầu đến hết khung giờ.</p>
              {price.voucherDiscount > 0 && <p className="mt-1 text-xs">Voucher: −{formatVnd(price.voucherDiscount)}</p>}
              {price.pointsDiscount > 0 && <p className="mt-1 text-xs">Điểm đổi: −{formatVnd(price.pointsDiscount)}</p>}
              {quote.isError && <Callout tone="error" className="mt-2">{quote.error.message} <Button type="button" size="sm" variant="ghost" disabled={quote.isFetching} onClick={() => void quote.refetch()}>Thử lại giá</Button></Callout>}
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
                disabled={create.isPending || selectedBooked || !validQuote || quote.isFetching || players > capacity}
                onClick={() => create.mutate()}
              >
                {create.isPending ? "Đang đặt…" : "Xác nhận đặt sân"}
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
                  {joinWaitlist.isPending ? "Đang xếp hàng…" : "Tham gia danh sách chờ"}
                </Button>
              )}
              {joinWaitlist.isError && <Callout tone="error" className="mt-2">{joinWaitlist.error.message}</Callout>}
              {waitlistDone && (
                <Callout tone="success" className="mt-2">
                  Đã tham gia danh sách chờ.
                </Callout>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

  </>);
}
