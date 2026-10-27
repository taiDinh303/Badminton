import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api, type Booking } from "../../../services/api";
import { experience } from "../../../services/api/experience";
import { useAuth } from "../../../store/auth";
import { Button, Callout, Field, SelectField } from "../../../components/ui";
import { dateInputValue, formatDay } from "../../../utils/format";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel, downloadText } from "./ExperienceUi";
import MediaGallery from "./MediaGallery";

function OpenTools({ booking: b }: { booking: Booking }) {
  const user = useAuth((s) => s.user);
  const owner = user?.id === b.userId;
  const [courtId, setCourt] = useState(b.courtId);
  const [slotId, setSlot] = useState(0);
  const [date, setDate] = useState(() => dateInputValue(b.date));
  const [reason, setReason] = useState("");
  const [kind, setKind] = useState(b.status === "NoShow" ? "NoShow" : "Other");
  const [caseReason, setCaseReason] = useState("");
  const [confirm, setConfirm] = useState(false);
  const action = useExperienceAction();
  const history = useQuery({
    queryKey: ["experience", "history", b.id],
    queryFn: () => experience.history(b.id),
  });
  const cases = useQuery({
    queryKey: ["experience", "cases", b.id],
    queryFn: () => experience.cases(b.id),
  });
  const courts = useQuery({
    queryKey: ["courts"],
    queryFn: () => api.courts.list(),
  });
  const slots = useQuery({
    queryKey: ["slots"],
    queryFn: () => api.timeSlots.list(),
  });
  const review = useQuery({
    queryKey: ["reviews", "booking", b.id],
    queryFn: () => api.reviews.forBooking(b.id),
    enabled: owner && b.status === "Completed",
  });
  return (
    <div className="space-y-4">
      <ActionStatus action={action} />
      <Link className="block text-primary underline" to="/calendar-sync">Đăng ký lịch tự đồng bộ khi đổi giờ hoặc hủy</Link>
      <Button
        variant="ghost"
        onClick={() =>
          action.mutate(async () =>
            downloadText(
              await experience.calendarFile(b.id),
              `booking-${b.id}.ics`,
              "text/calendar;charset=utf-8",
            ),
          )
        }
      >
        Tải ICS một lần
      </Button>
      <Panel title="Lịch sử đơn">
        {history.error && (
          <Callout tone="error">{history.error.message}</Callout>
        )}
        {history.data?.map((h, i) => (
          <p key={i} className="text-sm">
            {formatDay(h.at)} · {h.detail}
          </p>
        ))}
      </Panel>
      {b.groupId && (
        <Link
          className="text-primary underline"
          to={`/group-meetings?groupId=${b.groupId}&bookingId=${b.id}`}
        >
          Mời thành viên và xác nhận buổi chơi
        </Link>
      )}
      {b.groupId && <Link className="block text-primary underline" to={`/group-expenses?groupId=${b.groupId}&bookingId=${b.id}`}>Chia chi phí và theo dõi đóng góp</Link>}
      {owner && review.data && (
        <Panel title="Ảnh cho đánh giá của bạn">
          <MediaGallery reviewId={review.data.id} editable />
        </Panel>
      )}
      {["Pending", "Confirmed"].includes(b.status) && !b.checkedInAt && (
        <Panel title="Đổi lịch cùng giá">
          <form
            className="space-y-3"
            onSubmit={(e) => {
              e.preventDefault();
              action.mutate(async () => {
                await experience.reschedule(b.id, {
                  courtId,
                  slotId,
                  date,
                  reason,
                });
                setConfirm(false);
              });
            }}
          >
            <Field
              label="Ngày đổi sang"
              type="date"
              required
              value={date}
              onChange={(e) => {
                setDate(e.target.value);
                setConfirm(false);
              }}
            />
            <SelectField
              label="Sân mới"
              value={courtId}
              onChange={(e) => {
                setCourt(Number(e.target.value));
                setConfirm(false);
              }}
            >
              {courts.data?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </SelectField>
            <SelectField
              label="Khung giờ mới"
              required
              value={slotId}
              onChange={(e) => {
                setSlot(Number(e.target.value));
                setConfirm(false);
              }}
            >
              <option value={0}>Chọn giờ</option>
              {slots.data?.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.label}
                </option>
              ))}
            </SelectField>
            <Field
              label="Lý do đổi lịch"
              required
              minLength={5}
              maxLength={500}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
            <label className="flex items-start gap-2 text-sm">
              <input
                className="mt-1"
                type="checkbox"
                checked={confirm}
                required
                onChange={(e) => setConfirm(e.target.checked)}
              />
              Tôi xác nhận đổi lịch. Giá gốc phải bằng nhau; không kéo dài thời
              hạn giữ chỗ.
            </label>
            <Button disabled={action.isPending || !confirm || slotId === 0}>
              Xác nhận đổi lịch
            </Button>
          </form>
        </Panel>
      )}
      {owner && (
        <Panel title="Phản ánh về đơn">
          <form
            className="space-y-3"
            onSubmit={(e) => {
              e.preventDefault();
              action.mutate(() => experience.openCase(b.id, kind, caseReason));
            }}
          >
            <SelectField
              label="Loại phản ánh"
              value={kind}
              onChange={(e) => setKind(e.target.value)}
            >
              <option value="Other">Khác</option>
              <option value="Attendance">Check-in chưa chính xác</option>
              {b.status === "NoShow" && (
                <option value="NoShow">Khiếu nại vắng mặt</option>
              )}
            </SelectField>
            <Field
              label="Nội dung phản ánh"
              required
              minLength={5}
              maxLength={2000}
              value={caseReason}
              onChange={(e) => setCaseReason(e.target.value)}
            />
            <Button
              disabled={
                action.isPending || cases.data?.some((c) => c.status === "Open")
              }
            >
              Gửi phản ánh
            </Button>
          </form>
        </Panel>
      )}
      {cases.error && <Callout tone="error">{cases.error.message}</Callout>}
      {cases.data?.map((c) => (
        <p key={c.id} className="text-sm">
          Yêu cầu #{c.id}: {c.status} · {c.resolution ?? c.reason}
        </p>
      ))}
    </div>
  );
}
export default function BookingTools({ booking }: { booking: Booking }) {
  const [open, setOpen] = useState(false);
  return (
    <Panel title="Tiện ích và hỗ trợ đơn">
      <Button
        variant="ghost"
        aria-expanded={open}
        onClick={() => setOpen(!open)}
      >
        {open ? "Thu gọn tiện ích" : "Đổi lịch, xem lịch sử, gửi phản ánh"}
      </Button>
      {open && <OpenTools booking={booking} />}
    </Panel>
  );
}
