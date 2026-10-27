import { Link } from "react-router-dom";
import { Bell } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { experience } from "../../../services/api/experience";

export default function NotificationBell() {
  const count = useQuery({
    queryKey: ["experience", "unread"],
    queryFn: experience.unreadCount,
    staleTime: 60000,
    refetchInterval: 60000,
    retry: false,
  });
  const unread = typeof count.data === "number" ? count.data : 0;
  return (
    <Link
      to="/notifications"
      aria-label={
        count.isError
          ? "Mở thông báo (chưa tải được số chưa đọc)"
          : `Thông báo${unread ? `, ${unread} chưa đọc` : ""}`
      }
      className="relative flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-border"
    >
      <Bell className="h-5 w-5" />
      {unread > 0 && (
        <span
          aria-hidden="true"
          className="absolute -right-1 -top-1 rounded-full bg-primary px-1.5 text-xs text-white"
        >
          {unread > 99 ? "99+" : unread}
        </span>
      )}
    </Link>
  );
}
