import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api, type Booking } from "../../../services/api";
import { experience } from "../../../services/api/experience";
import { Button, Callout, Field, SelectField } from "../../../components/ui";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel } from "./ExperienceUi";

export default function DeskBookingForm({
  branchId,
  date,
}: {
  branchId: number;
  date: string;
}) {
  const [phone, setPhone] = useState("");
  const [lookup, setLookup] = useState("");
  const [courtId, setCourt] = useState(0);
  const [slotId, setSlot] = useState(0);
  const [players, setPlayers] = useState(1);
  const [requestKey, setKey] = useState(crypto.randomUUID());
  const [created, setCreated] = useState<Booking | null>(null);
  const action = useExperienceAction();
  const customer = useQuery({
    queryKey: ["experience", "customer", lookup],
    queryFn: () => experience.findCustomer(lookup),
    enabled: /^0\d{9}$/.test(lookup),
  });
  const courts = useQuery({
    queryKey: ["courts", branchId],
    queryFn: () => api.courts.list(branchId || undefined),
  });
  const slots = useQuery({
    queryKey: ["slots"],
    queryFn: () => api.timeSlots.list(),
  });
  const reset = () => {
    setCreated(null);
    setKey(crypto.randomUUID());
    action.reset();
  };
  return (
    <Panel title="Đặt hộ khách tại quầy">
      <form
        className="flex flex-wrap items-end gap-3"
        onSubmit={(e) => {
          e.preventDefault();
          setLookup(phone);
          reset();
        }}
      >
        <Field
          label="SĐT khách đã có tài khoản"
          required
          pattern="0[0-9]{9}"
          value={phone}
          onChange={(e) => {
            setPhone(e.target.value);
            setLookup("");
            reset();
          }}
        />
        <Button variant="ghost">Tìm khách</Button>
      </form>
      {customer.error && (
        <Callout tone="error">{customer.error.message}</Callout>
      )}
      {lookup && customer.isSuccess && !customer.data && (
        <p>
          Chưa tìm thấy khách hoạt động. Khách có thể đăng ký tại màn Đăng ký
          trước khi đặt hộ.
        </p>
      )}
      {customer.data && (
        <form
          className="space-y-3"
          onSubmit={(e) => {
            e.preventDefault();
            action.mutate(async () => {
              const b = await experience.desk({
                customerId: customer.data!.id,
                courtId,
                slotId,
                date,
                playerCount: players,
                requestKey,
              });
              setCreated(b);
            });
          }}
        >
          <p>
            Khách: <strong>{customer.data.name}</strong> ·{" "}
            {customer.data.isVerified
              ? "Đủ trạng thái xác minh"
              : "Chưa xác minh"}
          </p>
          <SelectField
            label="Sân đặt hộ"
            value={courtId}
            onChange={(e) => {
              setCourt(Number(e.target.value));
              reset();
            }}
          >
            <option value={0}>Chọn sân</option>
            {courts.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </SelectField>
          <SelectField
            label="Khung giờ đặt hộ"
            value={slotId}
            onChange={(e) => {
              setSlot(Number(e.target.value));
              reset();
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
            label="Số người tại quầy"
            type="number"
            min={1}
            max={courts.data?.find((c) => c.id === courtId)?.capacity ?? 10}
            value={players}
            onChange={(e) => {
              setPlayers(Number(e.target.value));
              reset();
            }}
          />
          <p className="text-sm text-muted">
            Ngày {date}. Tạo đơn Pending thuộc tài khoản khách, giữ nguyên luồng
            xác nhận tại quầy.
          </p>
          <Button
            disabled={
              action.isPending ||
              !customer.data.isVerified ||
              !courtId ||
              !slotId ||
              !!created
            }
          >
            Tạo đơn đặt hộ
          </Button>
        </form>
      )}
      <ActionStatus action={action} />
      {created && (
        <Link className="text-primary underline" to={`/bookings/${created.id}`}>
          Xem đơn {created.bookingCode}
        </Link>
      )}
    </Panel>
  );
}
