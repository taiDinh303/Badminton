import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "../../../store/auth";
import { api, type WaitlistEntryView } from "../../../services/api";
import { formatDay } from "../../../utils/format";
import {
  Busy,
  Callout,
  Card,
  Button,
} from "../../../components/ui";
import { ListChecks, X } from "lucide-react";

export default function WaitlistPage() {
  const user = useAuth((s) => s.user);
  const qc = useQueryClient();
  const [page, setPage] = useState(1);

  const { data: result, isLoading, isError } = useQuery({
    queryKey: ["waitlist", "mine", page],
    queryFn: () => api.waitlist.paged(page),
    enabled: user != null,
  });

  const entries = result?.items ?? [];
  const leave = useMutation({
    mutationFn: (id: number) => api.waitlist.leave(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["waitlist"] }),
  });

  if (isLoading && !entries.length)
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

      <div className="mt-3 flex items-center gap-3"><Button size="sm" variant="ghost" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>Trang trước</Button><span className="text-sm">Trang {page}/{Math.max(1, result?.totalPages ?? 1)}</span><Button size="sm" variant="ghost" disabled={page >= (result?.totalPages ?? 1)} onClick={() => setPage(p => p + 1)}>Trang sau</Button></div>
      {leave.isError && <Callout tone="error" className="mt-3">{leave.error.message}</Callout>}
      {entries.length === 0 ? (
        <Card className="mt-5 p-8 text-center text-sm text-muted">
          {page === 1 ? "Bạn chưa nằm trong danh sách chờ nào." : "Không có mục chờ trên trang này."}
          <Link to="/book" className="mt-3 block font-semibold text-primary">Chọn khung giờ đã đặt để tham gia chờ</Link>
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
                <span className="rounded-full bg-bg px-2 py-1 text-xs font-semibold">{{ Waiting: "Đang chờ", Promoted: "Đã tạo đơn", Cancelled: "Đã rời", Expired: "Hết hiệu lực", Declined: "Đã từ chối" }[e.status] ?? e.status}</span>
              </div>
              {e.declineReason && <p className="mt-2 text-sm text-muted">{e.declineReason}</p>}
              {e.promotedBookingId && <Link className="mt-3 block text-sm font-semibold text-primary" to={`/bookings/${e.promotedBookingId}`}>Xem đơn và thanh toán</Link>}
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
