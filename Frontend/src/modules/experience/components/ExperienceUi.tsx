import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { Button, Callout, Card } from "../../../components/ui";

export function Panel({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <Card className="mt-4 space-y-3 p-4">
      <h2 className="font-bold">{title}</h2>
      {children}
    </Card>
  );
}
export function ActionStatus({
  action,
}: {
  action: { isPending: boolean; isSuccess: boolean; error: Error | null };
}) {
  return (
    <div aria-live="polite">
      {action.isPending && <p>Đang xử lý…</p>}
      {action.error && <Callout tone="error">{action.error.message}</Callout>}
      {action.isSuccess && <Callout tone="success">Đã lưu thành công.</Callout>}
    </div>
  );
}
export function Pages({
  page,
  setPage,
  count,
}: {
  page: number;
  setPage: (page: number) => void;
  count: number;
}) {
  return (
    <div className="my-4 flex items-center gap-3">
      <Button
        variant="ghost"
        disabled={page === 1}
        onClick={() => setPage(page - 1)}
      >
        Trang trước
      </Button>
      <span>Trang {page}</span>
      <Button
        variant="ghost"
        disabled={count < 30}
        onClick={() => setPage(page + 1)}
      >
        Trang sau
      </Button>
    </div>
  );
}
export function ExperienceLinks() {
  return (
    <nav
      aria-label="Tiện ích đặt sân"
      className="my-4 flex flex-wrap gap-3 text-sm text-primary underline"
    >
      <Link to="/planner">Đặt nhiều giờ / định kỳ</Link>
      <Link to="/notifications">Thông báo</Link>
      <Link to="/favorites">Sân yêu thích</Link>
      <Link to="/group-meetings">Buổi chơi nhóm</Link>
      <Link to="/players">Tìm bạn chơi & tiện ích CLB</Link>
      <Link to="/calendar-sync">Lịch tự đồng bộ</Link>
      <Link to="/privacy">Yêu cầu dữ liệu cá nhân</Link>
      <Link to="/media-library">Ảnh của tôi</Link>
    </nav>
  );
}
export function downloadText(
  text: string,
  filename: string,
  type = "text/csv;charset=utf-8",
) {
  const url = URL.createObjectURL(
    new Blob([type.startsWith("text/csv") ? "\uFEFF" : "", text], { type }),
  );
  const link = document.createElement("a");
  link.href = url;
  link.download = filename;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
