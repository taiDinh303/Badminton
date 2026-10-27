import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import {
  Button,
  Callout,
  PageHeader,
  SelectField,
} from "../../../components/ui";
import { experience } from "../../../services/api/experience";
import { api } from "../../../services/api";
import { useAuth } from "../../../store/auth";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel } from "../components/ExperienceUi";

function Meeting({
  id,
  groupId,
  ownerId,
}: {
  id: number;
  groupId: number;
  ownerId: number;
}) {
  const userId = useAuth((s) => s.user?.id);
  const [ids, setIds] = useState<number[]>([]);
  const action = useExperienceAction();
  const rows = useQuery({
    queryKey: ["experience", "rsvps", id],
    queryFn: () => experience.rsvps(id),
  });
  const members = useQuery({
    queryKey: ["groups", "members", groupId],
    queryFn: () => api.groups.members(groupId),
    enabled: ownerId === userId,
  });
  const invited = rows.data?.some((r) => r.userId === userId);
  return (
    <>
      <Panel title="Xác nhận tham gia">
        {rows.error && <Callout tone="error">{rows.error.message}</Callout>}
        {rows.data?.map((r) => (
          <p key={r.userId}>
            {r.name} ·{" "}
            {r.attending === null
              ? "Chưa trả lời"
              : r.attending
                ? "Tham gia"
                : "Không tham gia"}
          </p>
        ))}
        {invited && (
          <div className="flex gap-3">
            <Button
              disabled={action.isPending}
              onClick={() => action.mutate(() => experience.rsvp(id, true))}
            >
              Tôi tham gia
            </Button>
            <Button
              variant="ghost"
              disabled={action.isPending}
              onClick={() => action.mutate(() => experience.rsvp(id, false))}
            >
              Tôi không tham gia
            </Button>
          </div>
        )}
      </Panel>
      {ownerId === userId && (
        <Panel title="Mời thành viên">
          <p className="text-sm text-muted">
            Chỉ mời thành viên hiện tại; số người nhận tham gia không vượt số
            người của đơn.
          </p>
          <form
            className="space-y-3"
            onSubmit={(e) => {
              e.preventDefault();
              action.mutate(() => experience.invite(id, ids));
            }}
          >
            {members.data?.map((member) => (
              <label key={member.userId} className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={ids.includes(member.userId)}
                  onChange={(e) =>
                    setIds((current) =>
                      e.target.checked
                        ? [...current, member.userId]
                        : current.filter((v) => v !== member.userId),
                    )
                  }
                />
                {member.fullName}
              </label>
            ))}
            <Button disabled={action.isPending || ids.length === 0}>
              Mời thành viên buổi chơi
            </Button>
          </form>
        </Panel>
      )}
      <ActionStatus action={action} />
    </>
  );
}
export default function GroupMeetingsPage() {
  const [params] = useSearchParams();
  const [groupId, setGroup] = useState(Number(params.get("groupId")) || 0);
  const [bookingId, setBooking] = useState(
    Number(params.get("bookingId")) || 0,
  );
  const groups = useQuery({
    queryKey: ["groups", "joined"],
    queryFn: api.groups.joined,
  });
  const meetings = useQuery({
    queryKey: ["experience", "meetings", groupId],
    queryFn: () => experience.meetings(groupId),
    enabled: groupId > 0,
  });
  const selected = meetings.data?.find((m) => m.id === bookingId);
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Buổi chơi nhóm"
        subtitle="Xác nhận tham gia theo từng đơn của nhóm"
      />
      <Panel title="Chọn buổi chơi">
        <SelectField
          label="Nhóm của bạn"
          value={groupId}
          onChange={(e) => {
            setGroup(Number(e.target.value));
            setBooking(0);
          }}
        >
          <option value={0}>Chọn nhóm</option>
          {groups.data?.map((g) => (
            <option key={g.id} value={g.id}>
              {g.name}
            </option>
          ))}
        </SelectField>
        <SelectField
          label="Lịch chơi của nhóm"
          value={bookingId}
          onChange={(e) => setBooking(Number(e.target.value))}
        >
          <option value={0}>Chọn buổi chơi</option>
          {meetings.data?.map((m) => (
            <option key={m.id} value={m.id}>
              {m.date} · {m.slot} · {m.code}
            </option>
          ))}
        </SelectField>
        {meetings.error && (
          <Callout tone="error">{meetings.error.message}</Callout>
        )}
      </Panel>
      {selected && (
        <Meeting
          key={selected.id}
          id={selected.id}
          groupId={groupId}
          ownerId={selected.ownerId}
        />
      )}
    </div>
  );
}
