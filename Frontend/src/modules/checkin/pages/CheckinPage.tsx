import { useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { motion, AnimatePresence } from "framer-motion";
import {
  ScanLine,
  Clock,
  CheckCircle2,
  Circle,
  Keyboard,
  QrCode,
  ListChecks,
} from "lucide-react";
import { api, type Court, type TimeSlot } from "../../../services/api";
import {
  Card,
  Callout,
  Busy,
  Button,
  PageHeader,
  Field,
  StatusBadge,
} from "../../../components/ui";
import { isStaff, useAuth } from "../../../store/auth";

/**
 * FEAT 9 — Staff check-in (docs 03 §3). Two ways to mark a guest in:
 *   1. Type the booking code (shown on the customer's "Đơn của tôi").
 *   2. Scan/paste the QR token.
 * Plus a slot roster: pick court + date + slot to see every unconfirmed
 * booking and check them in one by one.
 */
export default function CheckinPage() {
  const user = useAuth((s) => s.user);

  const [code, setCode] = useState("");
  const [token, setToken] = useState("");
  const [done, setDone] = useState<string | null>(null);

  const [courtId, setCourtId] = useState<number | "">("");
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [slotLabel, setSlotLabel] = useState("");

  const byCode = useMutation({
    mutationFn: () => api.checkin.byCode(code.trim()),
    onSuccess: (r) => {
      setDone(`Check-in đơn ${r.BookingId} thành công.`);
      setCode("");
    },
  });

  const byQr = useMutation({
    mutationFn: () => api.checkin.byQr(token.trim()),
    onSuccess: (r) => {
      setDone(`Check-in đơn ${r.BookingId} thành công.`);
      setToken("");
    },
  });

  const { data: courts = [] } = useQuery({
    queryKey: ["courts"],
    queryFn: () => api.courts.list(),
  });
  const { data: slotList = [] } = useQuery({
    queryKey: ["time-slots"],
    queryFn: api.timeSlots.list,
  });

  const slotOptions = useMemo(
    () =>
      [...slotList]
        .filter((s) => s.isActive)
        .sort((a, b) => a.startTime.localeCompare(b.startTime)),
    [slotList],
  );

  const slotRoster = useQuery({
    queryKey: ["checkin-slot", courtId, date, slotLabel],
    queryFn: () =>
      api.checkin.slot(courtId as number, date, slotLabel),
    enabled: courtId !== "" && slotLabel.trim() !== "",
  });

  if (!isStaff(user)) {
    return (
      <div className="p-4">
        <Callout tone="error">
          Chức năng check-in chỉ dành cho Manager / Admin.
        </Callout>
      </div>
    );
  }

  const slots = slotRoster.data ?? [];
  const selectedCourt = courts.find((c) => c.id === courtId);

  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Check-in"
        subtitle="Nhân viên xác nhận khách đến sân"
      />

      {/* Quick check-in */}
      <Card className="mt-5 p-4 md:p-5">
        <p className="mb-1 flex items-center gap-1.5 text-sm font-semibold">
          <ScanLine className="h-4 w-4 text-primary" /> Quét / nhập nhanh
        </p>
        <p className="mb-4 text-xs text-muted">
          Khách vào sân qua <strong>QR</strong> trong mục "Đơn của tôi", hoặc đọc
          <Keyboard className="mx-1 inline h-3 w-3" />
          mã đặt. Nhân viên nhập mã → bấm check-in.
        </p>

        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <Field
              label="Mã đặt (VD: BM-261018-XXXXXX)"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              placeholder="Nhập mã đặt"
            />
            <Button
              variant="accent"
              size="sm"
              className="mt-2 w-full"
              disabled={byCode.isPending || !code.trim()}
              onClick={() => byCode.mutate()}
            >
              <CheckCircle2 className="h-4 w-4" />
              {byCode.isPending ? "Đang check-in…" : "Check-in bằng mã"}
            </Button>
          </div>
          <div>
            <Field
              label="QR token (quét / paste)"
              value={token}
              onChange={(e) => setToken(e.target.value)}
              placeholder="Dữ liệu QR của đơn"
            />
            <Button
              variant="ghost"
              size="sm"
              className="mt-2 w-full"
              disabled={byQr.isPending || !token.trim()}
              onClick={() => byQr.mutate()}
            >
              <QrCode className="h-4 w-4" />
              {byQr.isPending ? "Đang check-in…" : "Check-in bằng QR"}
            </Button>
          </div>
        </div>

        <AnimatePresence>
          {done && (
            <motion.div
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0 }}
              transition={{ duration: 0.2 }}
            >
              <Callout tone="success" className="mt-4">
                {done}
              </Callout>
            </motion.div>
          )}
        </AnimatePresence>

        {(byCode.isError || byQr.isError) && (
          <Callout tone="error" className="mt-4">
            {byCode.isError
              ? byCode.error instanceof Error
                ? byCode.error.message
                : "Không check-in được."
              : byQr.error instanceof Error
                ? byQr.error.message
                : "Không check-in được."}
          </Callout>
        )}
      </Card>

      {/* Slot roster */}
      <Card className="mt-4 p-4 md:p-5">
        <p className="mb-1 flex items-center gap-1.5 text-sm font-semibold">
          <Clock className="h-4 w-4 text-primary" /> Khách theo khung giờ
        </p>
        <p className="mb-4 text-xs text-muted">
          Chọn sân + ngày + khung giờ để xem danh sách khách chưa check-in.
        </p>

        <div className="grid gap-3 sm:grid-cols-3">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-muted">
              Sân
            </label>
            <select
              value={courtId}
              onChange={(e) => setCourtId(e.target.value ? Number(e.target.value) : "")}
              aria-label="Chọn sân"
              className="h-11 w-full rounded-lg border border-border bg-card px-3 text-sm text-fg outline-none focus:border-primary"
            >
              <option value="">Chọn sân…</option>
              {courts.map((c: Court) => (
                <option key={c.id} value={c.id}>
                  {c.name} ({c.code})
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-muted">
              Ngày
            </label>
            <input
              type="date"
              value={date}
              min={new Date().toISOString().slice(0, 10)}
              onChange={(e) => setDate(e.target.value)}
              aria-label="Chọn ngày"
              className="h-11 w-full rounded-lg border border-border bg-card px-3 text-sm text-fg outline-none focus:border-primary"
            />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-muted">
              Khung giờ
            </label>
            <select
              value={slotLabel}
              onChange={(e) => setSlotLabel(e.target.value)}
              aria-label="Chọn khung giờ"
              className="h-11 w-full rounded-lg border border-border bg-card px-3 text-sm text-fg outline-none focus:border-primary disabled:opacity-50"
              disabled={courtId === ""}
            >
              <option value="">
                {courtId === "" ? "Chọn sân trước" : "Chọn khung giờ…"}
              </option>
              {slotOptions.map((s: TimeSlot) => (
                <option key={s.id} value={s.label}>
                  {s.label} ({s.startTime}–{s.endTime})
                </option>
              ))}
            </select>
          </div>
        </div>

        <div className="mt-4">
          {slotRoster.isFetching ? (
            <Busy label="Đang tải danh sách…" />
          ) : courtId !== "" && slotLabel ? (
            slots.length === 0 ? (
              <Callout tone="info">
                Chưa có đơn nào chưa check-in cho{" "}
                {selectedCourt?.name ?? "sân"} · {slotLabel}.
              </Callout>
            ) : (
              <div className="space-y-2">
                <p className="flex items-center gap-1.5 text-xs font-medium text-muted">
                  <ListChecks className="h-3.5 w-3.5" />
                  {slots.length} khách chưa check-in
                </p>
                {slots.map((s) => (
                  <div
                    key={s.id}
                    className="flex items-center justify-between rounded-lg border border-border bg-bg/40 p-3"
                  >
                    <div className="flex items-center gap-2.5">
                      {s.checkedInAt ? (
                        <CheckCircle2 className="h-4 w-4 text-state-success" />
                      ) : (
                        <Circle className="h-4 w-4 text-muted" />
                      )}
                      <span className="font-mono text-sm font-semibold tnum">
                        {s.bookingCode}
                      </span>
                      <StatusBadge status={s.status} size="sm" />
                    </div>
                    {s.checkedInAt ? (
                      <span className="text-xs text-muted tnum">
                        {new Date(s.checkedInAt).toLocaleTimeString("vi-VN")}
                      </span>
                    ) : (
                      <span className="text-xs text-muted">Chưa check-in</span>
                    )}
                  </div>
                ))}
              </div>
            )
          ) : (
            <p className="text-sm text-muted">
              Chọn sân + khung giờ để xem danh sách khách chưa check-in.
            </p>
          )}
        </div>
      </Card>
    </div>
  );
}
