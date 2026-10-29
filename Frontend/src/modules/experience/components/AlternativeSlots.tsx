import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, Callout } from "../../../components/ui";
import { experience } from "../../../services/api/experience";

export default function AlternativeSlots({
  courtId,
  date,
  slotId,
  onSelect,
}: {
  courtId: number;
  date: string;
  slotId: number;
  onSelect: (courtId: number, slotId: number) => void;
}) {
  const [open, setOpen] = useState(false);
  const choices = useQuery({
    queryKey: ["experience", "alternatives", courtId, date, slotId],
    queryFn: () => experience.alternatives(courtId, date, slotId),
    enabled: open,
  });
  return (
    <div className="mt-2">
      <Button
        size="sm"
        variant="ghost"
        className="w-full"
        onClick={() => setOpen(!open)}
      >
        Gợi ý sân / giờ thay thế
      </Button>
      {open && (
        <div className="mt-2 space-y-2">
          {choices.error && (
            <Callout tone="error">{choices.error.message}</Callout>
          )}
          {choices.data?.length === 0 && (
            <p className="text-sm">Chưa có lịch thay thế trong ngày.</p>
          )}
          {choices.data?.map((c) => (
            <Button
              key={`${c.courtId}:${c.slotId}`}
              variant="ghost"
              size="sm"
              className="w-full justify-start"
              onClick={() => {
                onSelect(c.courtId, c.slotId);
                setOpen(false);
              }}
            >
              {c.court} · {c.label}
            </Button>
          ))}
        </div>
      )}
    </div>
  );
}
