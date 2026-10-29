import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, Callout } from "../../../components/ui";
import { experience } from "../../../services/api/experience";
import { requestImage } from "../../../services/http";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus } from "./ExperienceUi";

function MediaImage({ id }: { id: number }) {
  const [source, setSource] = useState("");
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    let url = "";
    requestImage(id)
      .then((blob) => {
        if (active) {
          url = URL.createObjectURL(blob);
          setSource(url);
        }
      })
      .catch((reason: unknown) => {
        if (active)
          setError(
            reason instanceof Error ? reason.message : "Không tải được ảnh.",
          );
      });
    return () => {
      active = false;
      if (url) URL.revokeObjectURL(url);
    };
  }, [id]);
  return error ? (
    <p className="text-xs text-state-error">{error}</p>
  ) : source ? (
    <img
      className="h-24 w-full rounded object-cover"
      src={source}
      alt="Ảnh đã tải lên"
    />
  ) : (
    <p className="text-xs">Đang tải ảnh…</p>
  );
}

export default function MediaGallery({
  courtId,
  reviewId,
  editable = false,
  personal = false,
}: {
  courtId?: number;
  reviewId?: number;
  editable?: boolean;
  personal?: boolean;
}) {
  const [file, setFile] = useState<File | null>(null);
  const action = useExperienceAction();
  const photos = useQuery({
    queryKey: ["experience", "media", courtId, reviewId],
    queryFn: () => experience.media(courtId, reviewId),
  });
  return (
    <div className="space-y-3">
      {photos.error && <Callout tone="error">{photos.error.message}</Callout>}
      <div className="flex flex-wrap gap-3">
        {photos.data?.map((m) => (
          <div key={m.id} className="w-36 rounded border border-border p-2">
            <MediaImage id={m.id} />
            <p className="text-xs">
              Ảnh #{m.id} · {Math.ceil(m.length / 1024)} KB
            </p>
            {editable && (
              <div className="mt-2 flex flex-wrap gap-2">
                {personal && (
                  <Button
                    size="xs"
                    variant="ghost"
                    disabled={action.isPending}
                    onClick={() => action.mutate(() => experience.avatar(m.id))}
                  >
                    Dùng làm avatar
                  </Button>
                )}
                <Button
                  size="xs"
                  variant="danger"
                  disabled={action.isPending}
                  onClick={() => {
                    if (window.confirm("Xóa ảnh này?"))
                      action.mutate(() => experience.deleteMedia(m.id));
                  }}
                >
                  Xóa ảnh
                </Button>
              </div>
            )}
          </div>
        ))}
      </div>
      {editable && (
        <form
          className="flex flex-wrap items-end gap-3"
          onSubmit={(e) => {
            e.preventDefault();
            if (file)
              action.mutate(async () => {
                await experience.upload(file, courtId, reviewId);
                setFile(null);
              });
          }}
        >
          <label className="block min-w-0 text-sm">
            Ảnh PNG/JPEG, tối đa 2 MB
            <input
              className="mt-1 block w-full max-w-64 text-sm"
              type="file"
              accept="image/png,image/jpeg"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />
          </label>
          <Button disabled={!file || action.isPending}>Tải ảnh lên</Button>
        </form>
      )}
      <ActionStatus action={action} />
    </div>
  );
}
