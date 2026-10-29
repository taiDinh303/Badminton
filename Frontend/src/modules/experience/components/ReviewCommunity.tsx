import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Button, Field, Callout } from "../../../components/ui";
import { experience } from "../../../services/api/experience";
import { useAuth, isStaff } from "../../../store/auth";
import { useExperienceAction } from "../hooks/useExperienceAction";
import { ActionStatus } from "./ExperienceUi";
import MediaGallery from "./MediaGallery";

export default function ReviewCommunity({ reviewId }: { reviewId: number }) {
  const [open, setOpen] = useState(false);
  const user = useAuth((s) => s.user);
  return (
    <div className="mt-3">
      <Button
        size="xs"
        variant="ghost"
        aria-expanded={open}
        onClick={() => setOpen(!open)}
      >
        Ảnh, phản hồi và báo cáo đánh giá
      </Button>
      {open && <ReviewDetails reviewId={reviewId} staff={isStaff(user)} />}
    </div>
  );
}
function ReviewDetails({
  reviewId,
  staff,
}: {
  reviewId: number;
  staff: boolean;
}) {
  const [text, setText] = useState("");
  const [report, setReport] = useState("");
  const action = useExperienceAction();
  const reply = useQuery({
    queryKey: ["experience", "review-reply", reviewId],
    queryFn: () => experience.reviewReply(reviewId),
  });
  return (
    <div className="mt-3 space-y-3">
      {reply.error && <Callout tone="error">{reply.error.message}</Callout>}
      {reply.data && (
        <p className="rounded bg-bg p-3 text-sm">
          Phản hồi từ cơ sở: {reply.data.text}
        </p>
      )}
      <MediaGallery reviewId={reviewId} />
      {staff && (
        <form
          className="space-y-2"
          onSubmit={(e) => {
            e.preventDefault();
            action.mutate(() => experience.replyReview(reviewId, text));
          }}
        >
          <Field
            label="Phản hồi chính thức"
            value={text}
            required
            minLength={2}
            maxLength={2000}
            onChange={(e) => setText(e.target.value)}
          />
          <Button size="sm" disabled={action.isPending}>
            Lưu phản hồi
          </Button>
        </form>
      )}
      <form
        className="space-y-2"
        onSubmit={(e) => {
          e.preventDefault();
          action.mutate(() => experience.reportReview(reviewId, report));
        }}
      >
        <Field
          label="Lý do báo cáo đánh giá"
          value={report}
          required
          minLength={5}
          maxLength={1500}
          onChange={(e) => setReport(e.target.value)}
        />
        <Button size="sm" variant="ghost" disabled={action.isPending}>
          Báo cáo vi phạm
        </Button>
      </form>
      <ActionStatus action={action} />
    </div>
  );
}
