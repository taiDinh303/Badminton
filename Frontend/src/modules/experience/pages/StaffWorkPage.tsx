import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  Button,
  Callout,
  Field,
  PageHeader,
  SelectField,
} from "../../../components/ui";
import {
  experience,
  type HandoverView,
} from "../../../services/api/experience";
import { vietnamDate } from "../../../utils/format";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel, Pages } from "../components/ExperienceUi";
import BranchPicker from "../components/BranchPicker";

function HandoverResult({ row }: { row: HandoverView }) {
  const [outcome, setOutcome] = useState("");
  const action = useExperienceAction();
  return (
    <Panel
      title={`Việc #${row.id} · ${row.closedAt ? "Đã hoàn tất" : "Đang chờ"}`}
    >
      <p className="break-words">{row.text}</p>
      {row.resolution && <p>Kết quả: {row.resolution}</p>}
      {!row.closedAt && (
        <form
          className="space-y-2"
          onSubmit={(e) => {
            e.preventDefault();
            action.mutate(() => experience.closeHandover(row.id, outcome));
          }}
        >
          <Field
            label={`Kết quả bàn giao #${row.id}`}
            value={outcome}
            required
            minLength={5}
            maxLength={1500}
            onChange={(e) => setOutcome(e.target.value)}
          />
          <Button variant="ghost" disabled={action.isPending}>
            Hoàn tất việc bàn giao
          </Button>
        </form>
      )}
      <ActionStatus action={action} />
    </Panel>
  );
}
export default function StaffWorkPage() {
  const [branchId, setBranch] = useState(0);
  const [from, setFrom] = useState(vietnamDate());
  const [to, setTo] = useState(vietnamDate());
  const [staffId, setStaff] = useState(0);
  const [start, setStart] = useState("");
  const [end, setEnd] = useState("");
  const [note, setNote] = useState("");
  const [text, setText] = useState("");
  const [page, setPage] = useState(1);
  const action = useExperienceAction();
  const staff = useQuery({
    queryKey: ["experience", "staff", branchId],
    queryFn: () => experience.shiftStaff(branchId || undefined),
  });
  const shifts = useQuery({
    queryKey: ["experience", "shifts", from, to, branchId],
    queryFn: () => experience.shifts(from, to, branchId || undefined),
    enabled: !!from && !!to,
  });
  const handovers = useQuery({
    queryKey: ["experience", "handover", branchId, page],
    queryFn: () => experience.handovers(branchId || undefined, page),
  });
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Ca trực và bàn giao"
        subtitle="Giờ nhập theo Việt Nam; server kiểm ca giao nhau và phạm vi cơ sở"
      />
      <BranchPicker
        value={branchId}
        all={false}
        onChange={(id) => {
          setBranch(id);
          setStaff(0);
        }}
      />
      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        <Field
          label="Từ ngày trực"
          type="date"
          value={from}
          onChange={(e) => setFrom(e.target.value)}
        />
        <Field
          label="Đến ngày trực"
          type="date"
          value={to}
          onChange={(e) => setTo(e.target.value)}
        />
      </div>
      <Panel title="Thêm ca trực">
        <form
          className="space-y-3"
          onSubmit={(e) => {
            e.preventDefault();
            action.mutate(() =>
              experience.createShift({
                branchId,
                staffId,
                startsAtUtc: new Date(`${start}:00+07:00`).toISOString(),
                endsAtUtc: new Date(`${end}:00+07:00`).toISOString(),
                note,
              }),
            );
          }}
        >
          <SelectField
            label="Nhân viên trực"
            value={staffId}
            onChange={(e) => setStaff(Number(e.target.value))}
          >
            <option value={0}>Chọn nhân viên</option>
            {staff.data?.map((s) => (
              <option key={s.id} value={s.id}>
                {s.name}
              </option>
            ))}
          </SelectField>
          <Field
            label="Bắt đầu ca (giờ Việt Nam)"
            type="datetime-local"
            required
            value={start}
            onChange={(e) => setStart(e.target.value)}
          />
          <Field
            label="Kết thúc ca (giờ Việt Nam)"
            type="datetime-local"
            required
            value={end}
            onChange={(e) => setEnd(e.target.value)}
          />
          <Field
            label="Ghi chú ca"
            value={note}
            maxLength={500}
            onChange={(e) => setNote(e.target.value)}
          />
          <Button disabled={action.isPending || !branchId || !staffId}>
            Tạo ca trực
          </Button>
        </form>
      </Panel>
      <ActionStatus action={action} />
      {shifts.error && <Callout tone="error">{shifts.error.message}</Callout>}
      {shifts.data?.map((s) => (
        <Panel key={s.id} title={s.staffName}>
          <p>
            {new Date(s.startsAtUtc).toLocaleString("vi-VN", {
              timeZone: "Asia/Ho_Chi_Minh",
            })}{" "}
            –{" "}
            {new Date(s.endsAtUtc).toLocaleString("vi-VN", {
              timeZone: "Asia/Ho_Chi_Minh",
            })}
          </p>
          <p>{s.note}</p>
          <Button
            variant="danger"
            disabled={action.isPending}
            onClick={() => {
              if (window.confirm("Xóa ca trực này?"))
                action.mutate(() => experience.deleteShift(s.id));
            }}
          >
            Xóa ca trực
          </Button>
        </Panel>
      ))}
      <Panel title="Ghi việc bàn giao">
        <form
          className="space-y-3"
          onSubmit={(e) => {
            e.preventDefault();
            action.mutate(() => experience.handover(branchId, text));
          }}
        >
          <Field
            label="Nội dung bàn giao"
            value={text}
            required
            minLength={5}
            maxLength={2000}
            onChange={(e) => setText(e.target.value)}
          />
          <Button disabled={action.isPending || !branchId}>
            Thêm việc bàn giao
          </Button>
        </form>
      </Panel>
      {handovers.error && (
        <Callout tone="error">{handovers.error.message}</Callout>
      )}
      {handovers.data?.map((row) => (
        <HandoverResult key={row.id} row={row} />
      ))}
      <Pages
        page={page}
        setPage={setPage}
        count={handovers.data?.length ?? 0}
      />
    </div>
  );
}
