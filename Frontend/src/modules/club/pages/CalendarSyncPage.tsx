import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Button, Callout } from "../../../components/ui";
import { club, calendarFeedUrl } from "../../../services/api/club";
import {
  Shell,
  Panel,
  QueryStatus,
  ActionStatus,
  time,
} from "../components/ClubUi";
export default function CalendarSyncPage() {
  const queryClient = useQueryClient();
  const [url, setUrl] = useState("");
  const [copyStatus, setCopyStatus] = useState("");
  const query = useQuery({
    queryKey: ["club", "calendar-state"],
    queryFn: club.subscription,
    refetchInterval: 30000,
  });
  const action = useMutation({
    mutationFn: async (rotate: boolean) => {
      setCopyStatus("");
      if (rotate) {
        const { token } = await club.rotateCalendar();
        setUrl(calendarFeedUrl(token));
      } else {
        await club.revokeCalendar();
        setUrl("");
      }
      await queryClient.invalidateQueries({
        queryKey: ["club", "calendar-state"],
      });
    },
  });
  return (
    <Shell
      title="Lịch tự đồng bộ"
      subtitle="Đăng ký một lần để ứng dụng lịch tự lấy thay đổi giờ và trạng thái hủy của đơn đặt sân."
    >
      <Panel title="Liên kết đăng ký lịch riêng tư">
        <QueryStatus query={query} />
        {query.data?.active && (
          <Callout tone="info">
            {query.data.lastFetchedAtUtc
              ? `Liên kết được đọc gần nhất: ${time(query.data.lastFetchedAtUtc)}. Đây là lần tải feed, chưa chứng minh sự kiện đã hiện trên điện thoại.`
              : "Chưa ghi nhận ứng dụng lịch đọc liên kết. Hãy đăng ký URL trong ứng dụng lịch, không chỉ tải file."}
          </Callout>
        )}
        <p>
          {query.data?.active
            ? "Đang bật liên kết lịch."
            : "Chưa bật đồng bộ lịch."}
        </p>
        <p>
          Liên kết cho phép đọc lịch đặt sân của bạn. Chỉ lưu vào ứng dụng lịch
          tin cậy. Tạo liên kết mới sẽ vô hiệu liên kết cũ.
        </p>
        <div className="flex flex-wrap gap-3">
          <Button
            disabled={action.isPending}
            onClick={() => action.mutate(true)}
          >
            {query.data?.active ? "Tạo liên kết mới" : "Bật lịch tự đồng bộ"}
          </Button>
          {query.data?.active && (
            <Button
              variant="ghost"
              disabled={action.isPending}
              onClick={() => action.mutate(false)}
            >
              Thu hồi liên kết
            </Button>
          )}
        </div>
        {url && (
          <div className="space-y-3">
            <label className="block">
              Liên kết lịch (chỉ hiển thị lần này)
              <input
                aria-label="Liên kết lịch"
                className="mt-2 w-full rounded border border-line p-3"
                value={url}
                readOnly
                onFocus={(e) => e.currentTarget.select()}
              />
            </label>
            <Button variant="ghost" type="button" onClick={async () => {
              try {
                await navigator.clipboard.writeText(url);
                setCopyStatus("Đã sao chép liên kết lịch.");
              } catch {
                setCopyStatus("Chọn liên kết ở trên và sao chép thủ công.");
              }
            }}>Sao chép liên kết</Button>
            <p role="status">{copyStatus}</p>
            <a
              className="text-primary underline"
              href={url.replace(/^https?:/, "webcal:")}
              referrerPolicy="no-referrer"
            >
              Mở đăng ký trong ứng dụng lịch
            </a>
          </div>
        )}
        <ActionStatus action={action} />
      </Panel>
      <Panel title="Cách thêm vào điện thoại">
        <ol className="list-decimal space-y-2 pl-5">
          <li>Tạo và sao chép liên kết lịch.</li>
          <li>
            iPhone: mở Lịch → thêm lịch → thêm lịch đăng ký, rồi dán liên kết.
          </li>
          <li>
            Google Calendar: dùng chức năng thêm lịch từ URL trên bản web bằng
            tài khoản đang đồng bộ với điện thoại.
          </li>
        </ol>
        <Callout tone="info">
          Chọn đăng ký theo URL để nhận thay đổi. Tốc độ làm mới do ứng dụng
          lịch quyết định, có thể chậm; luôn xem đơn trong ứng dụng khi gần giờ
          chơi. Thu hồi chặn lần đọc tiếp theo, không xóa bản lịch đã lưu trên
          thiết bị.
        </Callout>
      </Panel>
    </Shell>
  );
}
