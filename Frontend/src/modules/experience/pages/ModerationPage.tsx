import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, Callout, Field, PageHeader } from "../../../components/ui";
import {
  experience,
  type ReviewReportView,
} from "../../../services/api/experience";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel, Pages } from "../components/ExperienceUi";
import BranchPicker from "../components/BranchPicker";

function ReportDecision({ row }: { row: ReviewReportView }) {
  const [reason, setReason] = useState("");
  const action = useExperienceAction();
  return (
    <Panel
      title={`Báo cáo #${row.id} · đánh giá #${row.reviewId} · ${row.status}`}
    >
      <p>
        Nội dung gốc ({row.rating}/5 sao): {row.comment ?? "Không có nhận xét"}
      </p>
      <p>Lý do báo cáo: {row.reason}</p>
      {row.resolution && <p>Quyết định: {row.resolution}</p>}
      <Field
        label={`Lý do kiểm duyệt #${row.id}`}
        required
        value={reason}
        minLength={5}
        maxLength={1500}
        onChange={(e) => setReason(e.target.value)}
      />
      <div className="flex flex-wrap gap-3">
        <Button
          variant="danger"
          disabled={action.isPending || reason.trim().length < 5}
          onClick={() =>
            action.mutate(() =>
              experience.moderateReview(row.reviewId, true, reason),
            )
          }
        >
          Ẩn đánh giá có lý do
        </Button>
        <Button
          variant="ghost"
          disabled={action.isPending || reason.trim().length < 5}
          onClick={() =>
            action.mutate(() =>
              experience.moderateReview(row.reviewId, false, reason),
            )
          }
        >
          Giữ / khôi phục đánh giá
        </Button>
      </div>
      <ActionStatus action={action} />
    </Panel>
  );
}
export default function ModerationPage() {
  const [branchId, setBranch] = useState(0);
  const [page, setPage] = useState(1);
  const rows = useQuery({
    queryKey: ["experience", "reports", branchId, page],
    queryFn: () => experience.reviewReports(branchId || undefined, page),
  });
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Kiểm duyệt đánh giá"
        subtitle="Ẩn hoặc khôi phục nội dung, giữ nguyên bản gốc và nhật ký"
      />
      <BranchPicker
        value={branchId}
        onChange={(id) => {
          setBranch(id);
          setPage(1);
        }}
      />
      {rows.error && <Callout tone="error">{rows.error.message}</Callout>}
      {rows.data?.length === 0 && <p>Chưa có báo cáo.</p>}
      {rows.data?.map((row) => (
        <ReportDecision key={row.id} row={row} />
      ))}
      <Pages page={page} setPage={setPage} count={rows.data?.length ?? 0} />
    </div>
  );
}
