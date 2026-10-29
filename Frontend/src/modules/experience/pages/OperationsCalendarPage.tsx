import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { PageHeader, Field, Callout, Button } from "../../../components/ui";
import { experience } from "../../../services/api/experience";
import { vietnamDate } from "../../../utils/format";
import BranchPicker from "../components/BranchPicker";
import DeskBookingForm from "../components/DeskBookingForm";

export default function OperationsCalendarPage() {
  const [branchId, setBranch] = useState(0);
  const [from, setFrom] = useState(vietnamDate());
  const [days, setDays] = useState(1);
  const end = new Date(`${from}T12:00:00+07:00`);
  end.setUTCDate(end.getUTCDate() + days - 1);
  const to = Number.isNaN(end.getTime()) ? from : vietnamDate(end);
  const rows = useQuery({
    queryKey: ["experience", "calendar", from, to, branchId],
    queryFn: () => experience.calendar(from, to, branchId || undefined),
    enabled: !!from,
  });
  const courtList = useQuery({
    queryKey: ["experience", "calendar-courts", branchId],
    queryFn: () => experience.operatingCourts(branchId || undefined),
  });
  const dates = from
    ? Array.from({ length: days }, (_, i) => {
        const date = new Date(`${from}T12:00:00+07:00`);
        date.setUTCDate(date.getUTCDate() + i);
        return vietnamDate(date);
      })
    : [];
  const courts =
    courtList.data?.map((c) => [c.courtId, c.courtName] as const) ?? [];
  return (
    <div className="mx-auto max-w-6xl p-4 md:p-6">
      <PageHeader
        title="Lịch vận hành ngày/tuần"
        subtitle="Theo dõi sân, thời gian và hiện diện; mở đơn để thao tác"
      />
      <div className="my-4 grid gap-3 sm:grid-cols-3">
        <BranchPicker value={branchId} onChange={setBranch} />
        <Field
          label="Ngày bắt đầu lịch"
          type="date"
          required
          value={from}
          onChange={(e) => setFrom(e.target.value)}
        />
        <div className="flex items-end gap-2">
          <Button
            variant={days === 1 ? "primary" : "ghost"}
            onClick={() => setDays(1)}
          >
            Ngày
          </Button>
          <Button
            variant={days === 7 ? "primary" : "ghost"}
            onClick={() => setDays(7)}
          >
            Tuần
          </Button>
        </div>
      </div>
      {rows.error && <Callout tone="error">{rows.error.message}</Callout>}
      {rows.isPending && <p role="status">Đang tải lịch…</p>}
      <div className="overflow-auto rounded border border-border">
        <table className="w-full min-w-[600px] text-sm">
          <thead>
            <tr>
              <th scope="col" className="p-3 text-left">
                Sân
              </th>
              {dates.map((d) => (
                <th scope="col" key={d} className="p-3 text-left">
                  {d}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {courts.map(([id, name]) => (
              <tr key={id} className="border-t border-border">
                <th scope="row" className="p-3 text-left">
                  {name}
                </th>
                {dates.map((d) => (
                  <td key={d} className="min-w-48 p-2 align-top">
                    {rows.data
                      ?.filter((r) => r.courtId === id && r.date === d)
                      .sort((a, b) => a.start.localeCompare(b.start))
                      .map((r) => (
                        <Link
                          key={r.id}
                          to={`/bookings/${r.id}`}
                          className="mb-2 block rounded border border-border bg-bg p-2"
                        >
                          <strong>
                            {r.start}–{r.end}
                          </strong>
                          <p>{r.customer}</p>
                          <p>
                            {r.status} ·{" "}
                            {r.checkedIn ? "Đã đến" : "Chưa check-in"}
                          </p>
                        </Link>
                      ))}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {rows.isSuccess && rows.data.length === 0 && (
        <p className="my-4">Không có đơn trong phạm vi này.</p>
      )}
      <DeskBookingForm
        key={`${branchId}:${from}`}
        branchId={branchId}
        date={from}
      />
    </div>
  );
}
