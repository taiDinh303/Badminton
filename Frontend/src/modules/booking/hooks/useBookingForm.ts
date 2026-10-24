import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError, type CourtAvailability, type Slot } from "../../../services/api";

export type BookingSelection = { court: CourtAvailability; slot: Slot };
export function isBooked(slot: Slot) { return slot.isLocked && ["Đã đặt", "Da duoc dat"].includes(slot.lockReason); }

export function useBookingForm(sel: BookingSelection | null, date: string, refresh: () => void, close: () => void) {
  const qc = useQueryClient();
  const attempt = useRef<{ payload: string; key: string } | null>(null);
  const [players, setPlayers] = useState(2);
  const [voucher, setVoucher] = useState("");
  const [done, setDone] = useState<string | null>(null);
  const [groupId, setGroupId] = useState<number | "">("");
  const [pointsToRedeem, setPointsToRedeem] = useState("");

  const create = useMutation({
    mutationFn: () => {
      if (!validQuote || quote.isFetching || players > capacity) throw new Error("Vui lòng đợi báo giá hợp lệ.");
      const payload = {
        courtId: sel!.court.courtId,
        date,
        slotId: sel!.slot.slotId,
        playerCount: players,
        voucherCode: voucher.trim() || null,
        groupId: groupId === "" ? null : groupId,
        pointsToRedeem: pointsToRedeem.trim() ? Number(pointsToRedeem) : null,
      };
      const serialized = JSON.stringify(payload);
      if (attempt.current?.payload !== serialized)
        attempt.current = { payload: serialized, key: crypto.randomUUID() };
      return api.booking.create({ ...payload, idempotencyKey: attempt.current.key });
    },
    onSuccess: (b) => {
      attempt.current = null;
      setDone(b.bookingCode);
      close();
      setVoucher("");
      setPointsToRedeem("");
      setGroupId("");
      void qc.invalidateQueries({ queryKey: ["account"] });
      void qc.invalidateQueries({ queryKey: ["my-bookings"] });
      refresh();
    },
  });

  const [waitlistDone, setWaitlistDone] = useState(false);
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

  const quote = useQuery({
    queryKey: ["booking-quote", sel?.court.courtId, sel?.slot.slotId, date, players, voucher.trim(), pointsToRedeem],
    enabled: !!sel && !selectedBooked,
    retry: false,
    queryFn: () => api.booking.quote({
      courtId: sel!.court.courtId, slotId: sel!.slot.slotId, date, playerCount: players,
      voucherCode: voucher.trim() || null,
      pointsToRedeem: pointsToRedeem.trim() ? Number(pointsToRedeem) : null,
    }),
  });
  const capacity = sel?.court.capacity ?? 10;
  const validQuote = !quote.isError && !!quote.data && Number.isFinite(quote.data.payable);
  const price = sel ? {
    base: sel.slot.isPeak ? sel.court.pricePeak : sel.court.priceLowPeak,
    total: validQuote ? quote.data!.payable : null,
    voucherDiscount: quote.data?.voucherDiscount ?? 0,
    pointsDiscount: quote.data?.pointsDiscount ?? 0,
    peak: sel.slot.isPeak,
  } : null;

  return { players, setPlayers, voucher, setVoucher, done, setDone, groupId, setGroupId,
    pointsToRedeem, setPointsToRedeem, create, joinWaitlist, quote, validQuote, capacity,
    waitlistDone, setWaitlistDone, createFailed, selectedBooked, price };
}
export type BookingForm = ReturnType<typeof useBookingForm>;
