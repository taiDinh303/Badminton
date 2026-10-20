import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "../../../store/auth";
import { api, type WaitlistEntryView } from "../../../services/api";
import { formatDay } from "../../../utils/format";
import {
  Busy,
  Callout,
  Card,
  StatusBadge,
  Button,
} from "../../../components/ui";
import { ListChecks, X } from "lucide-react";

/**
 * FEAT 2 — My waitlist (customer view). Shows every slot the customer has
 * joined the queue for, with status + "leave" action.
 */
export default function WaitlistPage() {
  const user = useAuth((s) => s.user);
  const qc = useQueryClient();

  const { data: entries = [], isFetching, isError } = useQuery({
    queryKey: ["waitlist", "mine"],
    queryFn: api.waitlist.mine,
    enabled: user != null,
  });

  const leave = useMutation({
    mutationFn: (id: number) => api.waitlist.leave(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["waitlist", "mine"] }),
  });

  if (isFetching && !entries.length)
    return <div className="p-4"><Busy label="Đang tải danh sách chờ…" /></div>;

  if (isError)
    return (
      <div className="p-4">
        <Callout tone="error">Không tải được danh sách chờ.</Callout>
      </div>
    );

  return (
    <div className="p-4">
      <h1 className="text-2xl font-bold tracking-tight">Danh sách chờ</h1>
      <p className="mt-1 text-sm text-muted">
        Khi slot được trả về, bạn sẽ được ưu tiên (FIFO) với đơn Pending mới.
      </p>

      {entries.length === 0 ? (
        <Card className="mt-5 p-8 text-center text-sm text-muted">
          Bạn chưa nằm trong danh sách chờ nào.
        </Card>
      ) : (
        <div className="mt-5 space-y-3">
          {entries.map((e: WaitlistEntryView) => (
            <Card key={e.id} className="p-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <p className="flex items-center gap-1.5 font-semibold">
                    <ListChecks className="h-4 w-4 text-primary" />
                    {e.courtName} · {e.slotLabel}
                  </p>
                  <p className="mt-0.5 text-xs text-muted tnum">
                    {formatDay(e.date)} · {e.playerCount} khách
                  </p>
                </div>
                <StatusBadge
                  status={
                    e.status === "Waiting" ? "Pending"
                    : e.status === "Promoted" ? "Confirmed"
                    : "Cancelled"
                  }
                  size="sm"
                />
              </div>
              {e.status === "Waiting" && (
                <div className="mt-3 flex justify-end">
                  <Button
                    variant="ghost"
                    size="xs"
                    onClick={() => leave.mutate(e.id)}
                    disabled={leave.isPending}
                  >
                    <X className="h-3.5 w-3.5" /> Rời danh sách
                  </Button>
                </div>
              )}
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
