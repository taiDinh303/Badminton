import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Button, Callout, Field, PageHeader } from "../../../components/ui";
import { experience, type CaseView } from "../../../services/api/experience";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel, Pages } from "../components/ExperienceUi";
import BranchPicker from "../components/BranchPicker";

function CaseDecision({ row }: { row: CaseView }) {
  const [resolution, setResolution] = useState("");
  const [verified, setVerified] = useState(false);
  const action = useExperienceAction();
  return (
    <Panel title={`Yêu cầu #${row.id} · ${row.kind} · ${row.status}`}>
      <Link
        className="text-primary underline"
        to={`/bookings/${row.bookingId}`}
      >
        Xem đơn #{row.bookingId}
      </Link>
      <p className="whitespace-pre-wrap break-words">{row.reason}</p>
      {row.resolution && <p>Kết quả: {row.resolution}</p>}
      {row.status === "Open" && (
        <>
          <Field
            label={`Kết quả xác minh #${row.id}`}
            required
            minLength={5}
            maxLength={2000}
            value={resolution}
            onChange={(e) => setResolution(e.target.value)}
          />
          {row.kind === "NoShow" && (
            <label className="flex items-start gap-2 text-sm">
              <input
                className="mt-1"
                type="checkbox"
                checked={verified}
                onChange={(e) => setVerified(e.target.checked)}
              />
              Đã xác minh khách thực sự đến chơi. Chấp nhận sẽ đính chính
              no-show, hoàn thành đơn và tính lại quyền lợi liên quan.
            </label>
          )}
          <div className="flex flex-wrap gap-3">
            <Button
              disabled={
                action.isPending ||
                resolution.trim().length < 5 ||
                (row.kind === "NoShow" && !verified)
              }
              onClick={() =>
                action.mutate(() =>
                  experience.resolveCase(row.id, true, resolution),
                )
              }
            >
              Chấp nhận sau xác minh
            </Button>
            <Button
              variant="ghost"
              disabled={action.isPending || resolution.trim().length < 5}
              onClick={() =>
                action.mutate(() =>
                  experience.resolveCase(row.id, false, resolution),
                )
              }
            >
              Từ chối có lý do
            </Button>
          </div>
        </>
      )}
      <ActionStatus action={action} />
    </Panel>
  );
}
export default function BookingCasesPage() {
  const [branchId, setBranch] = useState(0);
  const [page, setPage] = useState(1);
  const rows = useQuery({
    queryKey: ["experience", "cases", "staff", branchId, page],
    queryFn: () => experience.cases(undefined, branchId || undefined, page),
  });
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Phản ánh và khiếu nại"
        subtitle="Xác minh thông tin trước khi quyết định; mọi quyết định có nhật ký"
      />
      <BranchPicker
        value={branchId}
        onChange={(id) => {
          setBranch(id);
          setPage(1);
        }}
      />
      {rows.error && <Callout tone="error">{rows.error.message}</Callout>}
      {rows.data?.length === 0 && <p className="my-4">Không có yêu cầu.</p>}
      {rows.data?.map((row) => (
        <CaseDecision key={row.id} row={row} />
      ))}
      <Pages page={page} setPage={setPage} count={rows.data?.length ?? 0} />
    </div>
  );
}
