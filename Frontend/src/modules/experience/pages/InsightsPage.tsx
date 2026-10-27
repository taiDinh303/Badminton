import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, Callout, Field, PageHeader } from "../../../components/ui";
import { experience } from "../../../services/api/experience";
import { vietnamDate } from "../../../utils/format";
import { useAuth } from "../../../store/auth";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel, downloadText } from "../components/ExperienceUi";
import BranchPicker from "../components/BranchPicker";

export default function InsightsPage() {
  const [from, setFrom] = useState(vietnamDate());
  const [to, setTo] = useState(vietnamDate());
  const [branchId, setBranch] = useState(0);
  const action = useExperienceAction();
  const admin = useAuth((s) => s.user?.role === "Admin");
  const result = useQuery({
    queryKey: ["experience", "insights", from, to, branchId],
    queryFn: () => experience.insights(from, to, branchId || undefined),
    enabled: !!from && !!to,
  });
  const data = result.data;
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Khách hàng và xuất báo cáo"
        subtitle="Thống kê theo ngày chơi, cơ sở và giới hạn dữ liệu được phép"
      />
      <div className="my-4 grid gap-3 sm:grid-cols-3">
        <BranchPicker value={branchId} onChange={setBranch} />
        <Field
          label="Từ ngày báo cáo"
          type="date"
          value={from}
          onChange={(e) => setFrom(e.target.value)}
        />
        <Field
          label="Đến ngày báo cáo"
          type="date"
          value={to}
          onChange={(e) => setTo(e.target.value)}
        />
      </div>
      {result.error && <Callout tone="error">{result.error.message}</Callout>}
      {data && (
        <Panel title="Chỉ số trong kỳ">
          <dl className="grid grid-cols-2 gap-3 text-sm">
            {[
              ["Khách có đặt sân", data.customers],
              ["Khách mới", data.newCustomers],
              ["Khách quay lại", data.returningCustomers],
              ["Tổng đơn", data.bookings],
              ["Đơn no-show", data.noShows],
              ["Tỷ lệ no-show", `${data.noShowRate}%`],
              ["Lượt vào hàng chờ", data.waiting],
              ["Lượt được đôn chỗ", data.promoted],
            ].map(([label, value]) => (
              <div key={label}>
                <dt className="text-muted">{label}</dt>
                <dd className="text-lg font-bold">{value}</dd>
              </div>
            ))}
          </dl>
          <p className="text-xs text-muted">
            Khách quay lại đã có đơn trước kỳ trong cùng phạm vi cơ sở; tỷ lệ
            no-show dùng tổng đơn trong kỳ làm mẫu số.
          </p>
        </Panel>
      )}
      <Panel title="Xuất CSV dùng được trong Excel">
        <p className="text-sm text-muted">
          Tối đa 10.000 dòng mỗi lần; xuất đơn không chứa SĐT hoặc email khách.
        </p>
        <div className="flex flex-wrap gap-3">
          <Button
            disabled={action.isPending || !from || !to}
            onClick={() =>
              action.mutate(async () =>
                downloadText(
                  await experience.bookingExport(
                    from,
                    to,
                    branchId || undefined,
                  ),
                  `bookings-${from}-${to}.csv`,
                ),
              )
            }
          >
            Xuất đơn CSV
          </Button>
          {admin && (
            <Button
              variant="ghost"
              disabled={action.isPending || !from || !to}
              onClick={() =>
                action.mutate(async () =>
                  downloadText(
                    await experience.auditExport(from, to),
                    `audit-${from}-${to}.csv`,
                  ),
                )
              }
            >
              Xuất nhật ký CSV
            </Button>
          )}
        </div>
        <ActionStatus action={action} />
      </Panel>
    </div>
  );
}
