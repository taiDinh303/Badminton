import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api, type Booking } from "../../../services/api";
import { experience } from "../../../services/api/experience";
import {
  Button,
  Field,
  PageHeader,
  SelectField,
  Callout,
} from "../../../components/ui";
import { vietnamDate, formatVnd } from "../../../utils/format";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel } from "../components/ExperienceUi";

export default function PlannerPage() {
  const [courtId, setCourt] = useState(0);
  const [date, setDate] = useState(vietnamDate());
  const [slotIds, setSlots] = useState<number[]>([]);
  const [weeks, setWeeks] = useState(1);
  const [players, setPlayers] = useState(1);
  const [groupId, setGroup] = useState(0);
  const [created, setCreated] = useState<Booking[]>([]);
  const [requestKey, setKey] = useState(crypto.randomUUID());
  const action = useExperienceAction();
  const quote = useMutation({
    mutationFn: async () => {
      if (
        !courtId ||
        slotIds.length < 1 ||
        slotIds.length > 4 ||
        weeks < 1 ||
        weeks > 5
      )
        throw new Error("Chọn sân, 1–4 slot và 1–5 tuần hợp lệ.");
      const prices = await Promise.all(
        Array.from({ length: weeks }, (_, week) => {
          const day = new Date(`${date}T12:00:00+07:00`);
          day.setUTCDate(day.getUTCDate() + week * 7);
          return slotIds.map((slotId) =>
            api.booking.quote({
              courtId,
              slotId,
              date: vietnamDate(day),
              playerCount: players,
              groupId: groupId || null,
            }),
          );
        }).flat(),
      );
      return prices.reduce((sum, p) => sum + p.payable, 0);
    },
  });
  const courts = useQuery({
    queryKey: ["courts"],
    queryFn: () => api.courts.list(),
  });
  const slots = useQuery({
    queryKey: ["slots"],
    queryFn: () => api.timeSlots.list(),
  });
  const groups = useQuery({
    queryKey: ["groups", "mine"],
    queryFn: api.groups.mine,
  });
  const reset = () => {
    setKey(crypto.randomUUID());
    setCreated([]);
    action.reset();
    quote.reset();
  };
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Đặt nhiều giờ / định kỳ"
        subtitle="Mỗi khung giờ là một đơn; toàn bộ lịch phải hợp lệ mới tạo"
      />
      <Callout tone="info">
        Chọn các khung liên tiếp, tối đa 4 slot mỗi ngày và lịch trong 30 ngày
        tới. Mỗi đơn giữ chỗ 10 phút. Không tự xác nhận hoặc gia hạn vô hạn.
      </Callout>
      <Panel title="Lịch muốn đặt">
        <form
          className="space-y-3"
          onSubmit={(e) => {
            e.preventDefault();
            if (quote.data === undefined) {
              quote.mutate();
              return;
            }
            action.mutate(async () => {
              const rows = await experience.batch({
                courtId,
                firstDate: date,
                slotIds,
                weeks,
                playerCount: players,
                groupId: groupId || null,
                requestKey,
                expectedTotal: quote.data,
              });
              setCreated(rows);
            });
          }}
        >
          <SelectField
            label="Sân"
            required
            value={courtId}
            onChange={(e) => {
              setCourt(Number(e.target.value));
              reset();
            }}
          >
            <option value={0}>Chọn sân</option>
            {courts.data
              ?.filter((c) => c.isActive)
              .map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name} · cơ sở {c.branchId}
                </option>
              ))}
          </SelectField>
          <Field
            label="Ngày đầu tiên"
            type="date"
            required
            value={date}
            onChange={(e) => {
              setDate(e.target.value);
              reset();
            }}
          />
          <div className="flex flex-wrap gap-3">
            {slots.data
              ?.filter((s) => s.isActive)
              .map((s) => (
                <label
                  key={s.id}
                  className="flex items-center gap-2 rounded border border-border p-2 text-sm"
                >
                  <input
                    type="checkbox"
                    checked={slotIds.includes(s.id)}
                    onChange={(e) => {
                      setSlots((ids) =>
                        e.target.checked
                          ? [...ids, s.id]
                          : ids.filter((id) => id !== s.id),
                      );
                      reset();
                    }}
                  />
                  {s.label}
                </label>
              ))}
          </div>
          <Field
            label="Số tuần (1 = không lặp)"
            type="number"
            min={1}
            max={5}
            value={weeks}
            onChange={(e) => {
              setWeeks(Number(e.target.value));
              reset();
            }}
          />
          <Field
            label="Số người mỗi đơn"
            type="number"
            min={1}
            max={courts.data?.find((c) => c.id === courtId)?.capacity ?? 10}
            value={players}
            onChange={(e) => {
              setPlayers(Number(e.target.value));
              reset();
            }}
          />
          <SelectField
            label="Đặt cho"
            value={groupId}
            onChange={(e) => {
              setGroup(Number(e.target.value));
              reset();
            }}
          >
            <option value={0}>Cá nhân</option>
            {groups.data?.map((g) => (
              <option key={g.id} value={g.id}>
                {g.name}
              </option>
            ))}
          </SelectField>
          <p className="text-sm text-muted">
            Giá theo từng sân/khung giờ/ngày. Lượt đặt nhiều giờ chưa áp
            voucher/điểm.
          </p>
          {quote.data !== undefined && (
            <p className="font-bold">
              Tổng {weeks * slotIds.length} đơn: {formatVnd(quote.data)}
            </p>
          )}
          {quote.error && <Callout tone="error">{quote.error.message}</Callout>}
          <Button
            disabled={
              action.isPending ||
              quote.isPending ||
              !courtId ||
              slotIds.length === 0 ||
              created.length > 0
            }
          >
            {quote.data === undefined
              ? "Xem tổng giá trước khi đặt"
              : "Tạo toàn bộ lịch đã chọn"}
          </Button>
          {quote.data !== undefined && (
            <Button
              type="button"
              variant="ghost"
              disabled={action.isPending || created.length > 0}
              onClick={reset}
            >
              Tính lại giá
            </Button>
          )}
        </form>
        <ActionStatus action={action} />
        {courts.error && <Callout tone="error">{courts.error.message}</Callout>}
        {slots.error && <Callout tone="error">{slots.error.message}</Callout>}
        {created.map((b) => (
          <p key={b.id}>
            <Link className="text-primary underline" to={`/bookings/${b.id}`}>
              {b.bookingCode} · {b.date} · {b.slotLabel}
            </Link>
          </p>
        ))}
      </Panel>
    </div>
  );
}
