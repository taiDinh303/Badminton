import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, Callout, PageHeader } from "../../../components/ui";
import { experience } from "../../../services/api/experience";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel, Pages } from "../components/ExperienceUi";
import BranchPicker from "../components/BranchPicker";

export default function DeliveriesPage() {
  const [branchId, setBranch] = useState(0);
  const [page, setPage] = useState(1);
  const action = useExperienceAction();
  const rows = useQuery({
    queryKey: ["experience", "deliveries", branchId, page],
    queryFn: () => experience.deliveries(branchId || undefined, page),
  });
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Trạng thái gửi thông báo"
        subtitle="Delivered phản ánh relay đã nhận; không tự khẳng định người nhận đã đọc"
      />
      <BranchPicker
        value={branchId}
        onChange={(id) => {
          setBranch(id);
          setPage(1);
        }}
      />
      <ActionStatus action={action} />
      {rows.error && <Callout tone="error">{rows.error.message}</Callout>}
      {rows.data?.length === 0 && <p>Chưa có lượt gửi.</p>}
      {rows.data?.map((row) => (
        <Panel
          key={row.id}
          title={`#${row.id} · ${row.channel} · ${row.status}`}
        >
          <p className="text-sm">Đơn: {row.bookingId ?? "Ngoài đơn"}</p>
          <p className="text-sm">Số lần thử: {row.attempts}</p>
          {row.error && <p>{row.error}</p>}
          {row.status !== "Delivered" && (
            <Button
              variant="ghost"
              disabled={action.isPending}
              onClick={() =>
                action.mutate(() => experience.retryDelivery(row.id))
              }
            >
              Thử gửi lại
            </Button>
          )}
        </Panel>
      ))}
      <Pages page={page} setPage={setPage} count={rows.data?.length ?? 0} />
    </div>
  );
}
