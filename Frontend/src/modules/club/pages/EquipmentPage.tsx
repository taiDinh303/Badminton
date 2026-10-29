import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, SelectField, Callout } from "../../../components/ui";
import {
  club,
  type Equipment,
  type EquipmentOrder,
} from "../../../services/api/club";
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
  statusText,
} from "../components/ClubUi";

function ItemEditor({
  item,
  branchId,
}: {
  item?: Equipment;
  branchId: number;
}) {
  const action = useClubAction();
  return (
    <Panel title={item ? `Sửa kho · ${item.name}` : "Thêm dụng cụ / cầu"}>
      <Form
        pending={action.isPending || !branchId}
        label="Lưu tồn kho"
        submit={(f) =>
          action.mutate(() =>
            club.saveEquipment(item?.id ?? null, {
              branchId,
              name: text(f, "name"),
              rental: item ? item.rental : text(f, "rental") === "true",
              stock: num(f, "stock"),
              unitPrice: num(f, "price"),
              active: text(f, "active") === "true",
            }),
          )
        }
      >
        <Field label="Tên dụng cụ" name="name" value={item?.name} />
        {!item && (
          <label className="block">
            Loại
            <select name="rental" className="ml-3 rounded border p-2">
              <option value="true">Cho thuê vợt / dụng cụ</option>
              <option value="false">Bán cầu</option>
            </select>
          </label>
        )}
        <Field
          label="Tổng tồn kho vật lý"
          name="stock"
          type="number"
          min={0}
          max={100000}
          value={item?.stock ?? 0}
        />
        <Field
          label="Giá mỗi đơn vị (đ)"
          name="price"
          type="number"
          min={0}
          max={10000000}
          value={item?.unitPrice ?? 0}
        />
        <label className="flex gap-2">
          <input
            name="active"
            type="checkbox"
            value="true"
            defaultChecked={item?.active ?? true}
          />
          Đang nhận đặt
        </label>
      </Form>
      <ActionStatus action={action} />
    </Panel>
  );
}
function Reserve({ item }: { item: Equipment }) {
  const action = useClubAction();
  const [key, setKey] = useState(() => crypto.randomUUID());
  return (
    <Form
      pending={action.isPending || !item.active || item.available === 0}
      label={item.rental ? "Đặt thuê" : "Đặt mua cầu"}
      submit={(f) =>
        action.mutate(async () => {
          await club.reserveEquipment({
            itemId: item.id,
            quantity: num(f, "qty"),
            dueAtUtc: utc(f, "due"),
            requestKey: key,
          });
          setKey(crypto.randomUUID());
        })
      }
    >
      <Field
        label="Số lượng"
        name="qty"
        type="number"
        min={1}
        max={Math.min(item.available, 100)}
        value={1}
      />
      <Field
        label={
          item.rental
            ? "Hạn trả dự kiến (giờ Việt Nam)"
            : "Hẹn nhận (giờ Việt Nam)"
        }
        name="due"
        type="datetime-local"
      />
      <ActionStatus action={action} />
    </Form>
  );
}
function ConditionPanel({
  order,
  staff,
}: {
  order: EquipmentOrder;
  staff: boolean;
}) {
  const [open, setOpen] = useState(false);
  const action = useClubAction();
  const report = useQuery({
    queryKey: ["club", "condition", order.id],
    queryFn: () => club.condition(order.id),
    enabled: open,
  });
  return (
    <details onToggle={(e) => setOpen(e.currentTarget.open)}>
      <summary className="cursor-pointer text-primary">
        Biên bản dụng cụ #{order.id}
      </summary>
      {open && (
        <>
          <QueryStatus query={report} />
          {report.data ? (
            <p>
              {report.data.lost ? "Mất" : "Hư"} {report.data.unusableQuantity}{" "}
              dụng cụ · {report.data.reason} · {time(report.data.at)} · Nhân
              viên #{report.data.actorId} · Đề nghị đối chiếu{" "}
              {money(report.data.assessedAmount)} (chưa phải tiền đã thu)
            </p>
          ) : (
            staff &&
            order.status === "Issued" && (
              <Form
                pending={action.isPending}
                label={`Chốt biên bản #${order.id}`}
                submit={(f) =>
                  action.mutate(() =>
                    club.recordCondition(order.id, {
                      unusableQuantity: num(f, "quantity"),
                      lost: text(f, "lost") === "true",
                      assessedAmount: num(f, "amount"),
                      reason: text(f, "reason"),
                    }),
                  )
                }
              >
                <Field
                  label="Số lượng hư / mất"
                  name="quantity"
                  type="number"
                  min={1}
                  max={order.quantity}
                  value={1}
                />
                <label className="flex gap-2">
                  <input type="checkbox" name="lost" value="true" />
                  Mất dụng cụ (bỏ chọn = hư, đã nhận phần còn lại)
                </label>
                <Field
                  label="Khoản đề nghị đối chiếu (đ)"
                  name="amount"
                  type="number"
                  min={0}
                  max={100000000}
                  value={0}
                />
                <Field label="Lý do / tình trạng" name="reason" />
                <p>
                  Chốt một lần, giảm kho phần không dùng được; phần còn lại được
                  nhận trả. Không tự thu tiền hoặc áp phí phạt.
                </p>
              </Form>
            )
          )}
          <ActionStatus action={action} />
        </>
      )}
    </details>
  );
}
export default function EquipmentPage({ staff = false }: { staff?: boolean }) {
  const { branchId, control } = useBranch();
  const action = useClubAction();
  const [page, setPage] = useState(1);
  const [editId, setEdit] = useState(0);
  const items = useQuery({
    queryKey: ["club", "equipment", branchId],
    queryFn: () => club.equipment(branchId),
    enabled: branchId > 0,
  });
  const orders = useQuery({
    queryKey: ["club", "equipment-orders", staff, branchId, page],
    queryFn: () => club.equipmentOrders(staff, branchId, page),
    enabled: branchId > 0,
  });
  const edited = items.data?.find((x) => x.id === editId);
  return (
    <Shell
      staff={staff}
      title={staff ? "Kho & giao trả dụng cụ" : "Thuê dụng cụ & mua cầu"}
      subtitle="Giữ hàng tối đa 30 phút trước khi nhận; quá hạn tự nhả kho. Dụng cụ đã giao phải trả hoặc lập biên bản, không tự nhả kho khi quá hạn trả."
    >
      {control}
      <QueryStatus query={items} empty={items.data?.length === 0} />
      {items.data?.map((item) => (
        <Panel
          key={item.id}
          title={`${item.name} · ${item.rental ? "Cho thuê" : "Bán cầu"}`}
        >
          <p>
            {money(item.unitPrice)} / đơn vị · Còn {item.available}
            {staff && ` / ${item.stock}`} ·{" "}
            {item.active ? "Đang nhận đặt" : "Ngừng nhận đặt"}
          </p>
          {staff ? (
            <Button variant="ghost" onClick={() => setEdit(item.id)}>
              Sửa {item.name}
            </Button>
          ) : (
            <Reserve item={item} />
          )}
        </Panel>
      ))}
      {staff && (
        <>
          <SelectField
            label="Chọn mục kho cần sửa"
            value={editId}
            onChange={(e) => setEdit(Number(e.target.value))}
          >
            <option value={0}>Thêm mới</option>
            {items.data?.map((i) => (
              <option key={i.id} value={i.id}>
                {i.name}
              </option>
            ))}
          </SelectField>
          <ItemEditor
            key={`${branchId}:${editId}`}
            item={edited}
            branchId={branchId}
          />
        </>
      )}
      <Panel title={staff ? "Yêu cầu của khách" : "Yêu cầu của tôi"}>
        <QueryStatus query={orders} empty={orders.data?.length === 0} />
        {orders.data?.map((o) => (
          <div key={o.id} className="space-y-2 border-b border-line py-3">
            {o.overdue && (
              <Callout tone="warning">
                Quá hạn trả · Liên hệ cơ sở để xử lý. Chưa tự áp phí phạt.
              </Callout>
            )}
            {o.rental && ["Issued", "Lost", "Damaged"].includes(o.status) && (
              <ConditionPanel order={o} staff={staff} />
            )}
            <p>
              #{o.id} · {o.item} × {o.quantity} · {money(o.total)}
              {staff && ` · Khách #${o.userId}`}
            </p>
            <p>
              {statusText[o.status]} · {o.rental ? "Hạn trả" : "Hẹn nhận"}{" "}
              {time(o.dueAtUtc)}
              {o.returnedAtUtc && ` · Trả lúc ${time(o.returnedAtUtc)}`}
            </p>
            <div className="flex flex-wrap gap-3">
              {o.status === "Reserved" && (
                <>
                  <Button
                    variant="ghost"
                    disabled={action.isPending}
                    onClick={() =>
                      action.mutate(() => club.equipmentAction(o.id, "cancel"))
                    }
                  >
                    Hủy yêu cầu #{o.id}
                  </Button>
                  {staff && (
                    <Button
                      disabled={action.isPending}
                      onClick={() =>
                        action.mutate(() => club.equipmentAction(o.id, "issue"))
                      }
                    >
                      Xác nhận giao #{o.id}
                    </Button>
                  )}
                </>
              )}
              {staff && o.status === "Issued" && (
                <Button
                  disabled={action.isPending}
                  onClick={() =>
                    action.mutate(() => club.equipmentAction(o.id, "return"))
                  }
                >
                  Nhận trả #{o.id}
                </Button>
              )}
            </div>
          </div>
        ))}
        <Pages page={page} setPage={setPage} count={orders.data?.length ?? 0} />
      </Panel>
      <ActionStatus action={action} />
    </Shell>
  );
}
