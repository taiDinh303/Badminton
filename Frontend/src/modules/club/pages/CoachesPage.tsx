import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, SelectField } from "../../../components/ui";
import { club, type Coach, type CoachSlot } from "../../../services/api/club";
import {
  Shell,
  useBranch,
  Panel,
  QueryStatus,
  useClubAction,
  ActionStatus,
  money,
  time,
  Form,
  Field,
  Pages,
  text,
  num,
  utc,
  localDateTime,
  statusText,
} from "../components/ClubUi";

function CoachEditor({ coach, branchId }: { coach?: Coach; branchId: number }) {
  const action = useClubAction();
  return (
    <Panel title={coach ? `Hồ sơ ${coach.name}` : "Thêm huấn luyện viên"}>
      <Form
        pending={action.isPending || !branchId}
        label="Lưu HLV"
        submit={(f) =>
          action.mutate(() =>
            club.saveCoach(coach?.id ?? null, {
              branchId,
              name: text(f, "name"),
              bio: text(f, "bio"),
              active: text(f, "active") === "true",
            }),
          )
        }
      >
        <Field label="Tên huấn luyện viên" name="name" value={coach?.name} />
        <Field label="Giới thiệu / chuyên môn" name="bio" value={coach?.bio} />
        <label className="flex gap-2">
          <input
            name="active"
            type="checkbox"
            value="true"
            defaultChecked={coach?.active ?? true}
          />
          Đang nhận học viên
        </label>
      </Form>
      <ActionStatus action={action} />
    </Panel>
  );
}
function ReserveCoach({ slot }: { slot: CoachSlot }) {
  const action = useClubAction();
  const [key, setKey] = useState(() => crypto.randomUUID());
  return (
    <Form
      pending={action.isPending || slot.cancelled || !slot.available}
      label="Đặt buổi học"
      submit={(f) =>
        action.mutate(async () => {
          await club.reserveCoach({
            slotId: slot.id,
            people: num(f, "people"),
            requestKey: key,
          });
          setKey(crypto.randomUUID());
        })
      }
    >
      <Field
        label="Số học viên trong nhóm của bạn"
        name="people"
        type="number"
        min={1}
        max={slot.available}
        value={1}
      />
      <ActionStatus action={action} />
    </Form>
  );
}
export default function CoachesPage({ staff = false }: { staff?: boolean }) {
  const { branchId, control } = useBranch();
  const action = useClubAction();
  const [selected, setSelected] = useState(0);
  const [page, setPage] = useState(1);
  const [range, setRange] = useState(() => ({
    from: new Date().toISOString(),
    to: new Date(Date.now() + 30 * 86400000).toISOString(),
  }));
  const coaches = useQuery({
    queryKey: ["club", "coaches", branchId],
    queryFn: () => club.coaches(branchId),
    enabled: branchId > 0,
  });
  const coach = coaches.data?.find((x) => x.id === selected);
  const courtBookings = useQuery({
    queryKey: ["club", "coach-court-bookings", branchId],
    queryFn: () => club.courtBookings(branchId),
    enabled: staff && branchId > 0,
  });
  const slots = useQuery({
    queryKey: ["club", "coach-slots", coach?.id, range],
    queryFn: () => club.coachSlots(coach!.id, range.from, range.to),
    enabled: !!coach,
  });
  const reservations = useQuery({
    queryKey: ["club", "coaching", branchId, staff, page],
    queryFn: () => club.coaching(staff, branchId, page),
    enabled: branchId > 0,
  });
  return (
    <Shell
      staff={staff}
      title={staff ? "Quản lý huấn luyện viên" : "Đặt huấn luyện viên"}
      subtitle="Lớp chỉ nhận học viên khi có đơn sân đã xác nhận đúng giờ, cơ sở và sức chứa. Học phí thanh toán tại cơ sở."
    >
      {control}
      <SelectField
        label="Huấn luyện viên"
        value={coach?.id ?? 0}
        onChange={(e) => setSelected(Number(e.target.value))}
      >
        <option value={0}>{staff ? "Thêm HLV mới" : "Chọn HLV"}</option>
        {coaches.data?.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
            {!c.active ? " (Ngừng nhận)" : ""}
          </option>
        ))}
      </SelectField>
      <QueryStatus query={coaches} empty={coaches.data?.length === 0} />
      {coach && <Panel title={coach.name}><div className="club-tags"><span className="club-tag">Huấn luyện viên</span><span className="club-tag">{coach.active ? "Đang nhận học viên" : "Tạm ngừng nhận"}</span></div><p>{coach.bio || "Chọn lịch học bên dưới để xem buổi cá nhân hoặc nhóm còn chỗ."}</p></Panel>}
      {staff && (
        <CoachEditor
          key={`${branchId}:${coach?.id ?? 0}`}
          coach={coach}
          branchId={branchId}
        />
      )}
      {coach && (
        <>
          <Panel title="Khoảng lịch học (giờ Việt Nam)">
            <Form
              pending={false}
              label="Xem lịch trống"
              submit={(f) =>
                setRange({ from: utc(f, "from"), to: utc(f, "to") })
              }
            >
              <Field
                label="Từ"
                name="from"
                type="datetime-local"
                value={localDateTime()}
              />
              <Field
                label="Đến"
                name="to"
                type="datetime-local"
                value={localDateTime(new Date(Date.now() + 30 * 86400000))}
              />
            </Form>
          </Panel>
          <QueryStatus query={slots} empty={slots.data?.length === 0} />
          {slots.data?.map((s) => (
            <Panel
              key={s.id}
              title={`${time(s.startsAtUtc)} – ${time(s.endsAtUtc)}`}
            >
              <p>
                {s.capacity === 1 ? "Học cá nhân" : "Lớp nhóm"} · {s.available}/
                {s.capacity} chỗ trống · {money(s.pricePerPerson)} / người
                {s.cancelled && " · Đã hủy"}
              </p>
              <p>
                {s.courtReady
                  ? `Đã giữ sân · Đơn #${s.courtBookingId}`
                  : "Chưa có sân hợp lệ · Tạm ngừng nhận học viên"}
              </p>
              {staff && !s.cancelled && (
                <Form
                  pending={action.isPending}
                  label={`Gắn sân cho lớp #${s.id}`}
                  submit={(f) =>
                    action.mutate(() =>
                      club.attachCourt(s.id, num(f, "courtBooking")),
                    )
                  }
                >
                  <label className="block">
                    Đơn sân cho lớp #{s.id}
                    <select
                      name="courtBooking"
                      className="ml-2 max-w-full rounded border p-2"
                      required
                    >
                      <option value="">Chọn đơn sân</option>
                      {courtBookings.data?.map((b) => (
                        <option key={b.bookingId} value={b.bookingId}>
                          {b.code} · {b.court} · {time(b.startsAtUtc)} –{" "}
                          {time(b.endsAtUtc)}
                        </option>
                      ))}
                    </select>
                  </label>
                  <QueryStatus query={courtBookings} />
                  <p className="text-sm text-muted">
                    Chỉ dùng đơn sân cá nhân đã xác nhận do chính bạn đứng tên.
                    Đặt và xác nhận sân trước khi gắn cho lớp.
                  </p>
                </Form>
              )}
              {staff
                ? !s.cancelled && (
                    <Button
                      disabled={action.isPending}
                      variant="ghost"
                      onClick={() =>
                        action.mutate(() => club.cancelCoachSlot(s.id))
                      }
                    >
                      Hủy buổi học #{s.id}
                    </Button>
                  )
                : coach.active && <ReserveCoach slot={s} />}
            </Panel>
          ))}
          {staff && (
            <Panel title="Công bố lịch HLV">
              <Form
                pending={action.isPending}
                label="Thêm lịch học"
                submit={(f) =>
                  action.mutate(() =>
                    club.addCoachSlot(coach.id, {
                      startsAtUtc: utc(f, "start"),
                      endsAtUtc: utc(f, "end"),
                      capacity: num(f, "capacity"),
                      pricePerPerson: num(f, "price"),
                    }),
                  )
                }
              >
                <Field
                  label="Bắt đầu buổi học"
                  name="start"
                  type="datetime-local"
                />
                <Field
                  label="Kết thúc buổi học"
                  name="end"
                  type="datetime-local"
                />
                <Field
                  label="Số chỗ (1 = cá nhân, 2–20 = nhóm)"
                  name="capacity"
                  type="number"
                  min={1}
                  max={20}
                  value={1}
                />
                <Field
                  label="Học phí mỗi người (đ)"
                  name="price"
                  type="number"
                  min={0}
                  max={10000000}
                  value={0}
                />
              </Form>
            </Panel>
          )}
        </>
      )}
      <Panel title={staff ? "Danh sách học viên đặt chỗ" : "Buổi học của tôi"}>
        <QueryStatus
          query={reservations}
          empty={reservations.data?.length === 0}
        />
        {reservations.data?.map((r) => (
          <div key={r.id} className="space-y-2 border-b border-line py-3">
            <p>
              #{r.id} · {r.coach} · {r.people} người · {money(r.total)}
              {staff && ` · Khách #${r.userId}`}
            </p>
            <p>
              {time(r.startsAtUtc)} – {time(r.endsAtUtc)} ·{" "}
              {statusText[r.status]}
            </p>
            {r.status === "Reserved" && (
              <div className="flex flex-wrap gap-3">
                <Button
                  variant="ghost"
                  disabled={action.isPending}
                  onClick={() =>
                    action.mutate(() => club.coachingAction(r.id, "cancel"))
                  }
                >
                  Hủy đặt học #{r.id}
                </Button>
                {staff && (
                  <Button
                    disabled={action.isPending}
                    onClick={() =>
                      action.mutate(() => club.coachingAction(r.id, "complete"))
                    }
                  >
                    Hoàn thành #{r.id}
                  </Button>
                )}
              </div>
            )}
          </div>
        ))}
        <Pages
          page={page}
          setPage={setPage}
          count={reservations.data?.length ?? 0}
        />
      </Panel>
      <ActionStatus action={action} />
    </Shell>
  );
}
