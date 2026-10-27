import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useSearchParams } from "react-router-dom";
import { api } from "../../../services/api";
import {
  experience,
  type DisruptionView,
} from "../../../services/api/experience";
import {
  Button,
  Callout,
  Field,
  PageHeader,
  SelectField,
} from "../../../components/ui";
import { vietnamDate } from "../../../utils/format";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel, Pages } from "../components/ExperienceUi";
import BranchPicker from "../components/BranchPicker";

function ContactResult({ row }: { row: DisruptionView }) {
  const [outcome, setOutcome] = useState(row.outcome ?? "");
  const action = useExperienceAction();
  return (
    <Panel title={`${row.code} · ${row.customer}`}>
      <p>{row.message}</p>
      <p className="text-sm text-muted">
        {row.contactedAt
          ? "Đã ghi kết quả liên hệ"
          : "Chưa ghi kết quả liên hệ"}
      </p>
      <Link
        className="text-primary underline"
        to={`/bookings/${row.bookingId}`}
      >
        Xử lý đơn
      </Link>
      <form
        className="space-y-2"
        onSubmit={(e) => {
          e.preventDefault();
          action.mutate(() => experience.contact(row.id, outcome));
        }}
      >
        <Field
          label={`Kết quả liên hệ ${row.code}`}
          value={outcome}
          required
          minLength={5}
          maxLength={1500}
          onChange={(e) => setOutcome(e.target.value)}
        />
        <Button variant="ghost" disabled={action.isPending}>
          Lưu kết quả liên hệ
        </Button>
      </form>
      <ActionStatus action={action} />
    </Panel>
  );
}
export default function DisruptionsPage() {
  const [params] = useSearchParams();
  const [branchId, setBranch] = useState(Number(params.get("branchId")) || 0);
  const [courtId, setCourt] = useState(Number(params.get("courtId")) || 0);
  const [date, setDate] = useState(params.get("date") ?? vietnamDate());
  const [message, setMessage] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const [page, setPage] = useState(1);
  const [requestKey, setKey] = useState(crypto.randomUUID());
  const action = useExperienceAction();
  const courts = useQuery({
    queryKey: ["courts", branchId],
    queryFn: () => api.courts.list(branchId || undefined),
  });
  const contacts = useQuery({
    queryKey: ["experience", "disruptions", branchId, page],
    queryFn: () => experience.disruptions(branchId || undefined, page),
  });
  const reset = () => {
    setKey(crypto.randomUUID());
    setConfirmed(false);
    action.reset();
  };
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Sự cố và liên hệ khách"
        subtitle="Thông báo đến các đơn Pending/Confirmed trong phạm vi chọn; không tự hủy đơn"
      />
      <Panel title="Chuẩn bị thông báo sự cố / bảo trì">
        <form
          className="space-y-3"
          onSubmit={(e) => {
            e.preventDefault();
            action.mutate(() =>
              experience.announce({
                branchId,
                courtId: courtId || null,
                date,
                slotId: null,
                message,
                requestKey,
              }),
            );
          }}
        >
          <BranchPicker
            value={branchId}
            all={false}
            onChange={(id) => {
              setBranch(id);
              setCourt(0);
              reset();
            }}
          />
          <SelectField
            label="Phạm vi sân"
            value={courtId}
            onChange={(e) => {
              setCourt(Number(e.target.value));
              reset();
            }}
          >
            <option value={0}>Tất cả sân của cơ sở</option>
            {courts.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </SelectField>
          <Field
            label="Ngày bị ảnh hưởng"
            type="date"
            required
            value={date}
            onChange={(e) => {
              setDate(e.target.value);
              reset();
            }}
          />
          <Field
            label="Nội dung thông báo cho khách"
            value={message}
            required
            minLength={5}
            maxLength={1500}
            onChange={(e) => {
              setMessage(e.target.value);
              reset();
            }}
          />
          <label className="flex items-start gap-2 text-sm">
            <input
              type="checkbox"
              className="mt-1"
              checked={confirmed}
              required
              onChange={(e) => setConfirmed(e.target.checked)}
            />
            Tôi đã kiểm tra phạm vi và nội dung gửi đến khách.
          </label>
          <Button disabled={action.isPending || !branchId || !confirmed}>
            Gửi thông báo và lập danh sách liên hệ
          </Button>
        </form>
        <ActionStatus action={action} />
        {typeof action.data === "number" && (
          <p>
            Đã lập {action.data} mục liên hệ mới; thông báo ngoài ứng dụng còn
            phụ thuộc cấu hình kênh.
          </p>
        )}
      </Panel>
      {contacts.error && (
        <Callout tone="error">{contacts.error.message}</Callout>
      )}
      {contacts.data?.map((row) => (
        <ContactResult key={row.id} row={row} />
      ))}
      <Pages page={page} setPage={setPage} count={contacts.data?.length ?? 0} />
    </div>
  );
}
