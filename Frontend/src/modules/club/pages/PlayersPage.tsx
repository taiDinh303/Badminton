import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, SelectField } from "../../../components/ui";
import { club, type PlayerSession } from "../../../services/api/club";
import {
  Shell,
  useBranch,
  useClubAction,
  QueryStatus,
  Field,
  Form,
  Panel,
  Pages,
  ActionStatus,
  num,
  text,
  utc,
  localDateTime,
  time,
  statusText,
} from "../components/ClubUi";

function OwnerRequests({ session }: { session: PlayerSession }) {
  const action = useClubAction();
  const query = useQuery({
    queryKey: ["club", "session-members", session.id],
    queryFn: () => club.sessionMembers(session.id),
  });
  return (
    <div className="space-y-2">
      <QueryStatus query={query} empty={query.data?.length === 0} />
      {query.data?.map((m) => (
        <div key={m.userId} className="flex flex-wrap items-center gap-3">
          <span>
            {m.name} · {statusText[m.status]}
          </span>
          {m.status === "Requested" && (
            <>
              <Button
                disabled={action.isPending}
                onClick={() =>
                  action.mutate(() => club.decide(session.id, m.userId, true))
                }
              >
                Duyệt {m.name}
              </Button>
              <Button
                variant="ghost"
                disabled={action.isPending}
                onClick={() =>
                  action.mutate(() => club.decide(session.id, m.userId, false))
                }
              >
                Từ chối
              </Button>
            </>
          )}
        </div>
      ))}
      <ActionStatus action={action} />
    </div>
  );
}
export default function PlayersPage() {
  const { branchId, control, user } = useBranch();
  const action = useClubAction();
  const [page, setPage] = useState(1);
  const [area, setArea] = useState("");
  const [level, setLevel] = useState(0);
  const [range, setRange] = useState(() => ({
    fromUtc: new Date().toISOString(),
    toUtc: new Date(Date.now() + 30 * 86400000).toISOString(),
  }));
  const query = useQuery({
    queryKey: ["club", "players", branchId, area, level, range, page],
    queryFn: () =>
      club.sessions({
        branchId,
        area,
        level: level || undefined,
        ...range,
        page,
      }),
    enabled: branchId > 0,
  });
  return (
    <Shell
      title="Tìm bạn chơi"
      subtitle="Chọn trình độ, khu vực và khoảng giờ bạn rảnh. Giờ hiển thị theo Việt Nam."
    >
      {control}
      <Panel title="Tìm buổi phù hợp">
        <Form
          pending={action.isPending}
          label="Tìm bạn"
          submit={(f) => {
            setArea(text(f, "area"));
            setRange({ fromUtc: utc(f, "from"), toUtc: utc(f, "to") });
            setPage(1);
          }}
        >
          <Field label="Khu vực" name="area" required={false} />
          <SelectField
            label="Trình độ"
            value={level}
            onChange={(e) => {
              setLevel(Number(e.target.value));
              setPage(1);
            }}
          >
            <option value={0}>Tất cả trình độ</option>
            {[1, 2, 3, 4, 5].map((n) => (
              <option key={n} value={n}>
                Mức {n}
              </option>
            ))}
          </SelectField>
          <Field
            label="Rảnh từ"
            name="from"
            type="datetime-local"
            value={localDateTime()}
          />
          <Field
            label="Rảnh đến"
            name="to"
            type="datetime-local"
            value={localDateTime(new Date(Date.now() + 30 * 86400000))}
          />
        </Form>
      </Panel>
      <QueryStatus query={query} empty={query.data?.length === 0} />
      {query.data?.map((s) => (
        <Panel
          key={s.id}
          title={`${s.area} · Mức ${s.level} · ${s.accepted}/${s.capacity} người`}
        >
          <p>
            {s.owner} · {time(s.startsAtUtc)} – {time(s.endsAtUtc)}
          </p>
          <p>{s.note}</p>
          {s.ownerId === user?.id ? (
            <>
              <OwnerRequests session={s} />
              <Button
                variant="ghost"
                disabled={action.isPending}
                onClick={() =>
                  action.mutate(() => club.sessionAction(s.id, "close"))
                }
              >
                Đóng buổi chơi
              </Button>
            </>
          ) : (
            <div className="flex flex-wrap gap-3">
              <span>
                {s.myStatus ? statusText[s.myStatus] : "Chưa gửi yêu cầu"}
              </span>
              {s.myStatus === "Requested" || s.myStatus === "Accepted" ? (
                <Button
                  variant="ghost"
                  disabled={action.isPending}
                  onClick={() =>
                    action.mutate(() => club.sessionAction(s.id, "leave"))
                  }
                >
                  Rút tham gia
                </Button>
              ) : (
                <Button
                  disabled={action.isPending || s.accepted >= s.capacity}
                  onClick={() =>
                    action.mutate(() => club.sessionAction(s.id, "join"))
                  }
                >
                  Xin tham gia
                </Button>
              )}
            </div>
          )}
        </Panel>
      ))}
      <Pages page={page} setPage={setPage} count={query.data?.length ?? 0} />
      <Panel title="Mở buổi chơi">
        <p>
          Buổi tìm bạn chưa giữ sân. Chủ buổi cần đặt sân riêng sau khi thống
          nhất.
        </p>
        <Form
          pending={action.isPending || !branchId}
          label="Đăng buổi chơi"
          submit={(f) =>
            action.mutate(() =>
              club.createSession({
                branchId,
                area: text(f, "area"),
                level: num(f, "level"),
                startsAtUtc: utc(f, "start"),
                endsAtUtc: utc(f, "end"),
                capacity: num(f, "capacity"),
                note: text(f, "note"),
              }),
            )
          }
        >
          <Field label="Khu vực gặp mặt" name="area" />
          <Field
            label="Trình độ 1–5"
            name="level"
            type="number"
            min={1}
            max={5}
            value={2}
          />
          <Field label="Bắt đầu" name="start" type="datetime-local" />
          <Field label="Kết thúc" name="end" type="datetime-local" />
          <Field
            label="Số người gồm bạn"
            name="capacity"
            type="number"
            min={2}
            max={30}
            value={4}
          />
          <Field label="Lời nhắn" name="note" />
        </Form>
      </Panel>
      <ActionStatus action={action} />
    </Shell>
  );
}
