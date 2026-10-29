import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  Button,
  Callout,
  Field,
  PageHeader,
  SelectField,
} from "../../../components/ui";
import { experience } from "../../../services/api/experience";
import { useAuth } from "../../../store/auth";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus, Panel, Pages } from "../components/ExperienceUi";

export default function PrivacyPage({
  administration = false,
}: {
  administration?: boolean;
}) {
  const [kind, setKind] = useState("Correction");
  const [detail, setDetail] = useState("");
  const [page, setPage] = useState(1);
  const [resolution, setResolution] = useState("");
  const action = useExperienceAction();
  const user = useAuth((s) => s.user);
  const rows = useQuery({
    queryKey: ["experience", "privacy", administration, page],
    queryFn: () => experience.privacy(page, administration),
    enabled: !administration || user?.role === "Admin",
  });
  if (administration && user?.role !== "Admin")
    return (
      <Callout tone="error">
        Chỉ Admin được xử lý yêu cầu dữ liệu cá nhân.
      </Callout>
    );
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title={
          administration ? "Xử lý yêu cầu dữ liệu" : "Yêu cầu dữ liệu cá nhân"
        }
        subtitle="Yêu cầu sửa/xóa dữ liệu được xác minh trước khi xử lý"
      />
      <Callout tone="info">
        Gửi yêu cầu không tự xóa tài khoản hoặc lịch sử đặt sân. Quản trị viên
        phản hồi phạm vi đã xử lý và dữ liệu còn được giữ lại; không gửi mật
        khẩu hoặc giấy tờ nhạy cảm trong nội dung.
      </Callout>
      {!administration && (
        <Panel title="Gửi yêu cầu">
          <form
            className="space-y-3"
            onSubmit={(e) => {
              e.preventDefault();
              action.mutate(() => experience.createPrivacy(kind, detail));
            }}
          >
            <SelectField
              label="Loại yêu cầu"
              value={kind}
              onChange={(e) => setKind(e.target.value)}
            >
              <option value="Correction">Sửa dữ liệu</option>
              <option value="Deletion">Xóa dữ liệu</option>
            </SelectField>
            <Field
              label="Nội dung cần xử lý"
              value={detail}
              minLength={5}
              maxLength={2000}
              required
              onChange={(e) => setDetail(e.target.value)}
            />
            <Button disabled={action.isPending}>Gửi yêu cầu dữ liệu</Button>
          </form>
        </Panel>
      )}
      <ActionStatus action={action} />
      {rows.error && <Callout tone="error">{rows.error.message}</Callout>}
      {administration && (
        <Field
          label="Kết quả xác minh và xử lý"
          value={resolution}
          minLength={5}
          maxLength={2000}
          onChange={(e) => setResolution(e.target.value)}
        />
      )}
      {rows.data?.map((r) => (
        <Panel
          key={r.id}
          title={`Yêu cầu #${r.id} · ${r.kind === "Deletion" ? "Xóa" : "Sửa"} · ${r.status}`}
        >
          <p className="break-words">{r.detail}</p>
          {r.resolution && <p>Phản hồi: {r.resolution}</p>}
          {administration && r.status === "Open" && (
            <div className="flex flex-wrap gap-3">
              <Button
                disabled={action.isPending || resolution.trim().length < 5}
                onClick={() =>
                  action.mutate(() =>
                    experience.resolvePrivacy(r.id, true, resolution),
                  )
                }
              >
                Ghi nhận đã xử lý
              </Button>
              <Button
                variant="ghost"
                disabled={action.isPending || resolution.trim().length < 5}
                onClick={() =>
                  action.mutate(() =>
                    experience.resolvePrivacy(r.id, false, resolution),
                  )
                }
              >
                Từ chối có lý do
              </Button>
            </div>
          )}
        </Panel>
      ))}
      <Pages page={page} setPage={setPage} count={rows.data?.length ?? 0} />
    </div>
  );
}
