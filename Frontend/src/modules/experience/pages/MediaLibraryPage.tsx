import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { PageHeader, SelectField } from "../../../components/ui";
import { api } from "../../../services/api";
import { useAuth, isStaff } from "../../../store/auth";
import MediaGallery from "../components/MediaGallery";
import { Panel } from "../components/ExperienceUi";

export default function MediaLibraryPage() {
  const user = useAuth((s) => s.user);
  const staff = isStaff(user);
  const [courtId, setCourt] = useState(0);
  const courts = useQuery({
    queryKey: ["courts"],
    queryFn: () => api.courts.list(),
    enabled: staff,
  });
  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Ảnh của tôi"
        subtitle="Ảnh tải lên ban đầu là riêng tư; đặt làm avatar sẽ công khai ảnh đó"
      />
      <Panel title="Ảnh cá nhân">
        <MediaGallery personal editable />
      </Panel>
      {staff && (
        <Panel title="Ảnh sân">
          <SelectField
            label="Chọn sân để quản lý ảnh"
            value={courtId}
            onChange={(e) => setCourt(Number(e.target.value))}
          >
            <option value={0}>Chọn sân</option>
            {courts.data?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </SelectField>
          {courtId > 0 && (
            <MediaGallery key={courtId} courtId={courtId} editable />
          )}
        </Panel>
      )}
    </div>
  );
}
