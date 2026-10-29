import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, SelectField } from "../../../components/ui";
import { club, type Match } from "../../../services/api/club";
import {
  Shell,
  useBranch,
  Panel,
  QueryStatus,
  useClubAction,
  ActionStatus,
  time,
  Form,
  Field,
  Pages,
  text,
  num,
  utc,
  statusText,
} from "../components/ClubUi";
function MatchEditor({ id, match }: { id: number; match: Match }) {
  const action = useClubAction();
  const history = useQuery({
    queryKey: ["club", "score-history", id, match.id],
    queryFn: () => club.scoreHistory(id, match.id),
  });
  const [viewed, setViewed] = useState<{
    revision: number;
    scoreA: number;
    scoreB: number;
  } | null>(null);
  const [formVersion, setFormVersion] = useState(0);
  const latest = history.data?.at(-1);
  const currentRevision = latest?.revision ?? 0;
  const loadLatest = () => {
    setViewed({
      revision: currentRevision,
      scoreA: latest?.scoreA ?? match.scoreA ?? 0,
      scoreB: latest?.scoreB ?? match.scoreB ?? 0,
    });
    setFormVersion((v) => v + 1);
  };
  useEffect(() => {
    if (viewed === null && history.data)
      setViewed({
        revision: currentRevision,
        scoreA: latest?.scoreA ?? match.scoreA ?? 0,
        scoreB: latest?.scoreB ?? match.scoreB ?? 0,
      });
  }, [
    viewed,
    history.data,
    currentRevision,
    latest?.scoreA,
    latest?.scoreB,
    match.scoreA,
    match.scoreB,
  ]);
  return (
    <details className="my-3">
      <summary className="cursor-pointer text-primary">
        Xếp lịch / ghi kết quả trận #{match.id}
      </summary>
      <div className="space-y-4 py-3">
        {match.scoreA === null && (
          <Form
            pending={action.isPending}
            label={`Lưu lịch trận ${match.id}`}
            submit={(f) =>
              action.mutate(() =>
                club.schedule(id, match.id, {
                  startsAtUtc: utc(f, "start"),
                  endsAtUtc: utc(f, "end"),
                  location: text(f, "location"),
                }),
              )
            }
          >
            <Field label="Bắt đầu trận" name="start" type="datetime-local" />
            <Field label="Kết thúc trận" name="end" type="datetime-local" />
            <Field
              label="Tên sân thi đấu"
              name="location"
              value={match.location}
            />
          </Form>
        )}
        <Form
          key={`${viewed?.revision ?? -1}:${formVersion}`}
          pending={
            action.isPending ||
            viewed === null ||
            history.isPending ||
            !!history.error ||
            viewed.revision !== currentRevision
          }
          label={`Lưu kết quả trận ${match.id}`}
          submit={(f) =>
            action.mutate(() =>
              club.score(
                id,
                match.id,
                num(f, "scoreA"),
                num(f, "scoreB"),
                text(f, "reason"),
                viewed?.revision ?? 0,
              ),
            )
          }
        >
          <Field
            label="Lý do ghi / sửa kết quả"
            name="reason"
            required={match.scoreA !== null}
          />
          <Field
            label="Điểm đội A"
            name="scoreA"
            type="number"
            min={0}
            max={99}
            value={viewed?.scoreA ?? match.scoreA ?? 0}
          />
          <Field
            label="Điểm đội B"
            name="scoreB"
            type="number"
            min={0}
            max={99}
            value={viewed?.scoreB ?? match.scoreB ?? 0}
          />
        </Form>
        <QueryStatus query={history} />
        {viewed !== null && viewed.revision !== currentRevision && (
          <p role="alert">
            Có phiên bản kết quả mới. Bản nhập hiện tại chưa được ghi đè; nạp
            kết quả mới trước khi sửa.
          </p>
        )}
        <div className="flex flex-wrap gap-3">
          <Button
            variant="ghost"
            disabled={history.isFetching}
            onClick={() => void history.refetch()}
          >
            Kiểm tra lịch sử mới nhất
          </Button>
          <Button
            variant="ghost"
            disabled={!history.data || history.isFetching}
            onClick={loadLatest}
          >
            Nạp kết quả mới nhất
          </Button>
        </div>
        <p className="text-sm">
          Lịch sử kết quả được giữ lại; sửa điểm cần lý do và đúng phiên bản
          đang xem.
        </p>
        {history.data?.map((h) => (
          <p key={h.revision} className="text-sm">
            Lần {h.revision} · {h.oldScoreA ?? "–"}:{h.oldScoreB ?? "–"} →{" "}
            {h.scoreA}:{h.scoreB} · Nhân viên #{h.actorId} · {time(h.at)} ·{" "}
            {h.reason}
          </p>
        ))}
        <ActionStatus action={action} />
      </div>
    </details>
  );
}
function TournamentBoard({ id, staff }: { id: number; staff: boolean }) {
  const action = useClubAction();
  const query = useQuery({
    queryKey: ["club", "tournament", id],
    queryFn: () => club.tournament(id),
  });
  const data = query.data;
  const names = new Map(data?.teams.map((t) => [t.id, t.name]));
  return (
    <>
      <QueryStatus query={query} />
      {data && (
        <>
          <Panel
            title={`${data.tournament.name} · ${statusText[data.tournament.status]}`}
          >
            <p>
              Tối đa {data.tournament.maxTeams} đội. Bảng xếp hạng theo số trận
              thắng, hiệu số rồi thứ tự đăng ký; đồng điểm chưa phải quyết định
              vô địch.
            </p>
            {data.tournament.status === "Registration" && (
              <>
                <Form
                  pending={action.isPending}
                  label="Đăng ký đội"
                  submit={(f) =>
                    action.mutate(() =>
                      club.registerTeam(
                        id,
                        text(f, "name"),
                        text(f, "players"),
                      ),
                    )
                  }
                >
                  <Field label="Tên đội" name="name" />
                  <Field
                    label="Tên hiển thị các thành viên (không nhập SĐT)"
                    name="players"
                  />
                </Form>
                <Button
                  disabled={action.isPending}
                  variant="ghost"
                  onClick={() => action.mutate(() => club.withdrawTeam(id))}
                >
                  Rút đội của tôi
                </Button>
                {staff && (
                  <Form
                    pending={action.isPending}
                    label="Chia bảng vòng tròn"
                    submit={(f) =>
                      action.mutate(() => club.draw(id, num(f, "pools")))
                    }
                  >
                    <Field
                      label="Số bảng (mỗi bảng ít nhất 2 đội)"
                      name="pools"
                      type="number"
                      min={1}
                      max={8}
                      value={1}
                    />
                  </Form>
                )}
              </>
            )}
            {staff &&
              data.tournament.status !== "Completed" &&
              data.tournament.status !== "Cancelled" && (
                <Button
                  variant="ghost"
                  disabled={action.isPending}
                  onClick={() => action.mutate(() => club.cancelTournament(id))}
                >
                  Hủy giải đấu
                </Button>
              )}
          </Panel>
          <Panel title="Đội & xếp hạng">
            <QueryStatus query={query} empty={data.teams.length === 0} />
            {data.teams.map((t) => (
              <div key={t.id} className="club-team-row border-b border-line py-3">
                <p>
                  {t.pool ? `Bảng ${t.pool} · ` : ""}
                  {t.name} · {t.won} thắng / {t.played} trận · Hiệu số{" "}
                  {t.pointsFor - t.pointsAgainst}
                </p>
                <p className="text-sm text-muted">{t.players}</p>
              </div>
            ))}
          </Panel>
          <Panel title="Lịch đấu & kết quả">
            <p>
              Lịch giải là lịch tổ chức; nhân viên cần giữ sân riêng trong hệ
              thống đặt sân.
            </p>
            {data.matches.length === 0 && <p>Chưa chia bảng.</p>}
            {data.matches.map((m) => (
              <div key={m.id} className="club-match-card">
                <p>
                  Bảng {m.pool} · Vòng {m.round} · A: {names.get(m.teamAId)} —
                  B: {names.get(m.teamBId)}
                </p>
                <p>
                  {m.startsAtUtc
                    ? `${time(m.startsAtUtc)} – ${m.endsAtUtc ? time(m.endsAtUtc) : ""} · ${m.location}`
                    : "Chưa xếp giờ"}
                </p>
                <p>
                  {m.scoreA === null
                    ? "Chưa có kết quả"
                    : `${m.scoreA} – ${m.scoreB}`}
                </p>
                {staff && data.tournament.status !== "Cancelled" && (
                  <MatchEditor id={id} match={m} />
                )}
              </div>
            ))}
          </Panel>
          <ActionStatus action={action} />
        </>
      )}
    </>
  );
}
export default function TournamentsPage({
  staff = false,
}: {
  staff?: boolean;
}) {
  const { branchId, control } = useBranch();
  const action = useClubAction();
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState(0);
  const query = useQuery({
    queryKey: ["club", "tournaments", branchId, page],
    queryFn: () => club.tournaments(branchId, page),
    enabled: branchId > 0,
  });
  return (
    <Shell
      staff={staff}
      title="Giải đấu nhỏ"
      subtitle="Đăng ký đội, chia bảng vòng tròn, xếp lịch và ghi kết quả."
    >
      {control}
      <SelectField
        label="Giải đấu"
        value={selected}
        onChange={(e) => setSelected(Number(e.target.value))}
      >
        <option value={0}>Chọn giải</option>
        {query.data?.map((t) => (
          <option key={t.id} value={t.id}>
            {t.name} · {statusText[t.status]}
          </option>
        ))}
      </SelectField>
      <QueryStatus query={query} empty={query.data?.length === 0} />
      <Pages page={page} setPage={setPage} count={query.data?.length ?? 0} />
      {staff && (
        <Panel title="Tạo giải mới">
          <Form
            pending={action.isPending || !branchId}
            label="Tạo giải đấu"
            submit={(f) =>
              action.mutate(async () => {
                const id = await club.createTournament({
                  branchId,
                  name: text(f, "name"),
                  maxTeams: num(f, "max"),
                });
                setSelected(id);
              })
            }
          >
            <Field label="Tên giải" name="name" />
            <Field
              label="Số đội tối đa"
              name="max"
              type="number"
              min={2}
              max={32}
              value={8}
            />
          </Form>
        </Panel>
      )}
      {selected > 0 && (
        <TournamentBoard key={selected} id={selected} staff={staff} />
      )}
      <ActionStatus action={action} />
    </Shell>
  );
}
