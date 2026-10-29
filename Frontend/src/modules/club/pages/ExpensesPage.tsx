import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { Button, Callout, SelectField } from "../../../components/ui";
import { api } from "../../../services/api";
import { useAuth } from "../../../store/auth";
import { club } from "../../../services/api/club";
import {
  Shell,
  Panel,
  QueryStatus,
  useClubAction,
  ActionStatus,
  money,
  Pages,
} from "../components/ClubUi";
function ExpenseEditor({
  bookingId,
  groupId,
  total,
}: {
  bookingId: number;
  groupId: number;
  total: number;
}) {
  const userId = useAuth((s) => s.user?.id);
  const action = useClubAction();
  const [selected, setSelected] = useState<number[]>([]);
  const [byTime, setByTime] = useState(false);
  const [minutes, setMinutes] = useState<Record<number, number>>({});
  const weights = selected.map((id) => ({
    id,
    weight: byTime ? (minutes[id] ?? 60) : 1,
  }));
  const denominator = weights.reduce((sum, x) => sum + x.weight, 0);
  const preview = weights.map((x) => ({
    ...x,
    amount: denominator ? Math.floor((total * x.weight) / denominator) : 0,
    remainder: denominator ? (total * x.weight) % denominator : 0,
  }));
  const extra = total - preview.reduce((sum, x) => sum + x.amount, 0);
  if (denominator)
    [...preview]
      .sort((a, b) => b.remainder - a.remainder || a.id - b.id)
      .slice(0, extra)
      .forEach((x) => x.amount++);
  const members = useQuery({
    queryKey: ["groups", "members", groupId],
    queryFn: () => api.groups.members(groupId),
  });
  const query = useQuery({
    queryKey: ["club", "expense", bookingId],
    queryFn: () => club.expense(bookingId),
  });
  const expense = query.data;
  const collected = expense?.shares.filter((s) => s.paidAt).reduce((sum, s) => sum + s.amount, 0) ?? 0;
  return (
    <Panel title={`Chi phí đơn #${bookingId}`}>
      <QueryStatus query={query} />
      {expense ? (
        <>
          <dl className="club-expense-summary">
            <div><dt>Tiền sân cần chia</dt><dd>{money(expense.total)}</dd></div>
            <div><dt>Đã đóng góp</dt><dd>{money(collected)}</dd></div>
            <div><dt>Còn cần đóng</dt><dd>{money(Math.max(0, expense.total - collected))}</dd></div>
          </dl>
          <progress className="club-payment-progress" aria-label="Tiến độ đóng góp" max={expense.total || 1} value={collected} />
          <p>
            {expense.splitMethod === "Attendance"
              ? "Theo phút tham dự · "
              : "Chia đều · "}
            Tổng {money(expense.total)} · Đã đóng{" "}
            {money(
              expense.shares
                .filter((s) => s.paidAt)
                .reduce((sum, s) => sum + s.amount, 0),
            )}
          </p>
          {expense.bookingCancelled && (
            <Callout tone="warning">
              Đơn đã hủy/hoàn. Nhóm tự đối chiếu và hoàn lại đóng góp; bảng này
              giữ lịch sử số tiền đã chốt.
            </Callout>
          )}
          {expense.shares.map((s) => (
            <div
              key={s.userId}
              className="flex flex-wrap items-center justify-between gap-3 border-b border-line py-3"
            >
              <span>
                {s.name} · {money(s.amount)} ·{" "}
                {s.attendedMinutes != null && `${s.attendedMinutes} phút · `}
                {s.paidAt ? "Đã đóng" : "Chưa đóng"}
              </span>
              {expense.ownerId === userId && (
                <Button
                  disabled={
                    action.isPending || (expense.bookingCancelled && !s.paidAt)
                  }
                  variant="ghost"
                  onClick={() =>
                    action.mutate(() =>
                      club.contribution(bookingId, s.userId, !s.paidAt),
                    )
                  }
                >
                  {s.paidAt ? "Bỏ xác nhận" : "Xác nhận đã đóng"}
                </Button>
              )}
            </div>
          ))}
        </>
      ) : (
        !query.isPending &&
        !query.error && (
          <>
            <p>
              Chia đều tiền sân sau giảm giá; phần lẻ phân bổ theo thứ tự thành
              viên. Người đặt chốt một lần.
            </p>
            <QueryStatus query={members} />
            <label className="flex gap-2">
              <input
                type="checkbox"
                checked={byTime}
                onChange={(e) => setByTime(e.target.checked)}
              />
              Chia theo phút tham dự
            </label>
            {members.data?.map((m) => (
              <div key={m.userId} className="flex flex-wrap gap-2">
                <label className="flex gap-2">
                  <input
                    type="checkbox"
                    checked={selected.includes(m.userId)}
                    onChange={(e) =>
                      setSelected((ids) =>
                        e.target.checked
                          ? [...ids, m.userId]
                          : ids.filter((id) => id !== m.userId),
                      )
                    }
                  />
                  {m.fullName}
                </label>
                {byTime && selected.includes(m.userId) && (
                  <input
                    aria-label={`Phút tham dự ${m.fullName}`}
                    className="w-24 rounded border p-1"
                    type="number"
                    min={0}
                    max={1440}
                    value={minutes[m.userId] ?? 60}
                    onChange={(e) =>
                      setMinutes((v) => ({
                        ...v,
                        [m.userId]: Math.max(
                          0,
                          Math.min(1440, Number(e.target.value)),
                        ),
                      }))
                    }
                  />
                )}
              </div>
            ))}
            {preview.map((p) => (
              <p key={p.id}>
                {members.data?.find((m) => m.userId === p.id)?.fullName}: dự
                kiến {money(p.amount)}
              </p>
            ))}
            <Button
              disabled={
                action.isPending || selected.length === 0 || denominator === 0
              }
              onClick={() =>
                action.mutate(() =>
                  byTime
                    ? club.splitAttendance(
                        bookingId,
                        selected.map((userId) => ({
                          userId,
                          minutes: minutes[userId] ?? 60,
                        })),
                      )
                    : club.split(bookingId, selected),
                )
              }
            >
              {byTime ? "Chốt theo thời lượng" : "Chốt chia đều"}
            </Button>
          </>
        )
      )}
      <ActionStatus action={action} />
    </Panel>
  );
}
export default function ExpensesPage() {
  const [params] = useSearchParams();
  const [groupId, setGroup] = useState(Number(params.get("groupId")) || 0);
  const [bookingId, setBooking] = useState(
    Number(params.get("bookingId")) || 0,
  );
  const groups = useQuery({
    queryKey: ["groups", "joined"],
    queryFn: api.groups.joined,
  });
  const [page, setPage] = useState(1);
  const bookings = useQuery({
    queryKey: ["club", "expense-bookings", groupId, page],
    queryFn: () => club.expenseBookings(groupId, page),
    enabled: groupId > 0,
  });
  return (
    <Shell
      title="Chia chi phí nhóm"
      subtitle="Theo dõi đóng góp nội bộ; người đặt sân xác nhận ai đã đóng."
    >
      <Panel title="Chọn đơn nhóm">
        <SelectField
          label="Nhóm"
          value={groupId}
          onChange={(e) => {
            setGroup(Number(e.target.value));
            setBooking(0);
            setPage(1);
          }}
        >
          <option value={0}>Chọn nhóm</option>
          {groups.data?.map((g) => (
            <option key={g.id} value={g.id}>
              {g.name}
            </option>
          ))}
        </SelectField>
        <QueryStatus query={groups} />
        {groupId > 0 && (
          <>
            <SelectField
              label="Đơn đặt sân của nhóm"
              value={bookingId}
              onChange={(e) => setBooking(Number(e.target.value))}
            >
              <option value={0}>Chọn đơn</option>
              {bookings.data?.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.code} · {b.date} · {money(b.payableAmount)} · {b.status}
                </option>
              ))}
            </SelectField>
            <QueryStatus query={bookings} empty={bookings.data?.length === 0} />
            <Pages
              page={page}
              setPage={setPage}
              count={bookings.data?.length ?? 0}
            />
          </>
        )}
      </Panel>
      {groupId > 0 &&
        bookingId > 0 &&
        bookings.data?.some((b) => b.id === bookingId) && (
          <ExpenseEditor
            key={`${groupId}:${bookingId}`}
            groupId={groupId}
            bookingId={bookingId}
            total={
              bookings.data.find((b) => b.id === bookingId)?.payableAmount ?? 0
            }
          />
        )}
    </Shell>
  );
}
