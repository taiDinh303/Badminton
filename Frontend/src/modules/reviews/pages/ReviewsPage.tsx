import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Star, MessageSquare } from "lucide-react";
import { api, type Court, type ReviewView } from "../../../services/api";
import { formatDay } from "../../../utils/format";
import {
  Card,
  Callout,
  Busy,
  Button,
  PageHeader,
  EmptyState,
} from "../../../components/ui";

/** Row of 5 star glyphs (SVG, filled vs outline) — accessible label. */
function Stars({ rating, size = "h-4 w-4" }: { rating: number; size?: string }) {
  return (
    <div className="flex gap-0.5" role="img" aria-label={`${rating}/5 sao`}>
      {[1, 2, 3, 4, 5].map((i) => (
        <Star
          key={i}
          className={size + " " + (i <= rating ? "fill-accent text-accent" : "text-muted/40")}
        />
      ))}
    </div>
  );
}

/**
 * FEAT 8 — Reviews (docs 03 §7): public per-court review list + average.
 * Submission happens on a Completed booking's detail page.
 */
export default function ReviewsPage() {
  const qc = useQueryClient();
  const [courtId, setCourtId] = useState<number | null>(null);

  const { data: courts = [], isLoading: fetchingCourts, error: courtsError } = useQuery({
    queryKey: ["courts"],
    queryFn: () => api.courts.list(),
  });

  const { data: reviews = [], isLoading: fetchingReviews, isError, error } = useQuery({
    queryKey: ["reviews", courtId, courts.map((c) => c.id)],
    queryFn: async () => courtId != null
      ? qc.fetchQuery({ queryKey: ["reviews", "court", courtId], queryFn: () => api.reviews.forCourt(courtId) })
      : (await Promise.all(courts.map((c) => qc.fetchQuery({ queryKey: ["reviews", "court", c.id], queryFn: () => api.reviews.forCourt(c.id) })))).flat()
          .sort((a, b) => b.createdAt.localeCompare(a.createdAt)),
    enabled: courts.length > 0,
  });

  const avg =
    reviews.length > 0
      ? Math.round(
          (reviews.reduce((s, r) => s + r.rating, 0) / reviews.length) * 10,
        ) / 10
      : null;

  const selectedCourt = courts.find((c) => c.id === courtId);

  return (
    <div className="mx-auto max-w-3xl p-4 md:p-6">
      <PageHeader
        title="Đánh giá sân"
        subtitle="Xem đánh giá từ khách đã tập tại từng sân"
      />

      {/* Court selector */}
      <div className="mt-5 flex flex-wrap gap-2">
        <Button
          variant="ghost"
          size="sm"
          className={courtId == null ? "border-primary text-primary" : ""}
          onClick={() => setCourtId(null)}
        >
          Tất cả
        </Button>
        {courts.map((c: Court) => (
          <Button
            key={c.id}
            variant="ghost"
            size="sm"
            className={courtId === c.id ? "border-primary text-primary" : ""}
            onClick={() => setCourtId(c.id)}
          >
            {c.name}
          </Button>
        ))}
      </div>

      {fetchingCourts ? (
        <Busy label="Đang tải sân…" />
      ) : (
        <>
          {selectedCourt && avg != null && (
            <Card className="mt-4 flex items-center gap-4 p-4">
              <span className="text-3xl font-bold tnum">{avg.toFixed(1)}</span>
              <div>
                <Stars rating={Math.round(avg)} />
                <p className="mt-1 text-xs text-muted">
                  {reviews.length} đánh giá · {selectedCourt.name}
                </p>
              </div>
            </Card>
          )}

          <div className="mt-4">
            {(isError || courtsError) && <Callout tone="error">Không tải được đánh giá. {(error ?? courtsError)?.message}</Callout>}
            {fetchingReviews && !reviews.length && (
              <Busy label="Đang tải đánh giá…" />
            )}
            {!fetchingReviews && !isError && !courtsError && reviews.length === 0 && (
              <EmptyState
                icon={<MessageSquare className="h-5 w-5" />}
                title="Chưa có đánh giá nào"
                hint="Đánh giá được tạo sau khi một đơn đã hoàn thành."
              >
                <Link to="/bookings" className="mt-2 text-sm font-semibold text-primary">Đánh giá đơn đã hoàn thành</Link>
              </EmptyState>
            )}

            <div className="space-y-3">
              {reviews.map((r: ReviewView) => (
                <Card key={r.id} className="p-4">
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2.5">
                      <span className="flex h-9 w-9 items-center justify-center rounded-full bg-primary/10 text-sm font-bold text-primary">
                        {r.userName.charAt(0).toUpperCase()}
                      </span>
                      <div>
                        <p className="text-sm font-semibold">{r.userName} · {r.courtName}</p>
                        <p className="text-[11px] text-muted tnum">
                          {formatDay(r.createdAt.slice(0, 10))}
                        </p>
                      </div>
                    </div>
                    <Stars rating={r.rating} />
                  </div>
                  {r.comment && (
                    <p className="mt-3 text-sm text-muted">{r.comment}</p>
                  )}
                  {r.flaggedRefunded && (
                    <p className="mt-2 text-[11px] font-medium text-state-warning">
                      Đơn này đã được hoàn tiền
                    </p>
                  )}
                </Card>
              ))}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
