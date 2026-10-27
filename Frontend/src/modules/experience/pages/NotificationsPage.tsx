import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Button, Callout, Field, PageHeader } from "../../../components/ui";
import { experience, type Preferences } from "../../../services/api/experience";
import { formatDay } from "../../../utils/format";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel, Pages } from "../components/ExperienceUi";

function PreferenceForm({ initial }: { initial: Preferences }) {
  const [value, setValue] = useState(initial);
  const action = useExperienceAction();
  return (
    <Panel title="Tùy chọn nhắc lịch">
      <form
        className="space-y-3"
        onSubmit={(e) => {
          e.preventDefault();
          action.mutate(() => experience.savePreferences(value));
        }}
      >
        <label className="flex items-center gap-2">
          <input
            type="checkbox"
            checked={value.remindersEnabled}
            onChange={(e) =>
              setValue({ ...value, remindersEnabled: e.target.checked })
            }
          />
          Nhắc trước giờ chơi
        </label>
        <Field
          label="Số phút trước giờ chơi"
          type="number"
          min={15}
          max={1440}
          value={value.minutesBefore}
          onChange={(e) =>
            setValue({ ...value, minutesBefore: Number(e.target.value) })
          }
        />
        <p className="text-sm text-muted">
          Thông báo luôn có trong ứng dụng. Các kênh ngoài chỉ gửi khi hệ thống
          đã cấu hình và tài khoản có địa chỉ liên hệ phù hợp.
        </p>
        {(["email", "sms", "zalo"] as const).map((channel) => (
          <label className="mr-4 inline-flex items-center gap-2" key={channel}>
            <input
              type="checkbox"
              checked={value[channel]}
              onChange={(e) =>
                setValue({ ...value, [channel]: e.target.checked })
              }
            />
            {channel.toUpperCase()}
          </label>
        ))}
        <div>
          <Button disabled={action.isPending}>Lưu tùy chọn</Button>
        </div>
      </form>
      <ActionStatus action={action} />
    </Panel>
  );
}
export default function NotificationsPage() {
  const [page, setPage] = useState(1);
  const action = useExperienceAction();
  const messages = useQuery({
    queryKey: ["experience", "inbox", page],
    queryFn: () => experience.inbox(page),
  });
  const preferences = useQuery({
    queryKey: ["experience", "preferences"],
    queryFn: experience.preferences,
  });
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Thông báo"
        subtitle="Lịch chơi, hàng chờ và thông báo từ cơ sở"
      />
      {messages.isPending && <p role="status">Đang tải thông báo…</p>}
      {messages.error && (
        <Callout tone="error">{messages.error.message}</Callout>
      )}
      {messages.data?.length === 0 && (
        <p className="my-4 text-muted">Chưa có thông báo.</p>
      )}
      {messages.data?.map((m) => (
        <Panel key={m.id} title={m.title}>
          <p className="whitespace-pre-wrap break-words text-sm">{m.body}</p>
          <p className="text-xs text-muted">
            {formatDay(m.createdAt)} · {m.readAt ? "Đã đọc" : "Chưa đọc"}
          </p>
          <div className="flex flex-wrap gap-3">
            {m.bookingId && (
              <Link
                className="text-primary underline"
                to={`/bookings/${m.bookingId}`}
              >
                Xem đơn
              </Link>
            )}
            {!m.readAt && (
              <Button
                size="sm"
                variant="ghost"
                disabled={action.isPending}
                onClick={() => action.mutate(() => experience.read(m.id))}
              >
                Đánh dấu đã đọc
              </Button>
            )}
          </div>
        </Panel>
      ))}
      <Pages page={page} setPage={setPage} count={messages.data?.length ?? 0} />
      <ActionStatus action={action} />
      {preferences.data && <PreferenceForm initial={preferences.data} />}
      {preferences.error && (
        <Callout tone="error">{preferences.error.message}</Callout>
      )}
    </div>
  );
}
