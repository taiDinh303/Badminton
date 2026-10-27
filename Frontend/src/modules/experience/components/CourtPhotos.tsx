import { useState } from "react";
import { Button } from "../../../components/ui";
import MediaGallery from "./MediaGallery";

export default function CourtPhotos({ courtId }: { courtId: number }) {
  const [open, setOpen] = useState(false);
  return (
    <section className="mt-3" aria-label="Ảnh sân đã chọn">
      <Button size="xs" variant="ghost" aria-expanded={open} onClick={() => setOpen(!open)}>
        {open ? "Thu gọn ảnh sân" : "Xem ảnh sân"}
      </Button>
      {open && <div className="mt-2 max-h-48 overflow-auto"><MediaGallery courtId={courtId} /></div>}
    </section>
  );
}
