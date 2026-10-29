import { Home, CalendarDays, ListChecks, User, LayoutDashboard, Dumbbell, Ticket, Clock, Users, ScrollText, Crown, Hourglass, Star, ScanLine, Inbox } from "lucide-react";

export const customerItems = [
  { to: "/dashboard", label: "Trang chính", icon: Home, end: true },
  { to: "/book", label: "Đặt sân", icon: CalendarDays },
  { to: "/bookings", label: "Đơn của tôi", icon: ListChecks },
  { to: "/waitlist", label: "Chờ", icon: Hourglass },
  { to: "/groups", label: "Nhóm", icon: Users },
  { to: "/players", label: "Câu lạc bộ", icon: Users },
  { to: "/reviews", label: "Đánh giá", icon: Star },
  { to: "/notifications", label: "Thông báo", icon: Inbox },
  { to: "/account", label: "Tài khoản", icon: User },
];

// Mobile bottom nav keeps only the essentials so 5 items fit (docs 07 §3).
export const mobileItems = [
  { to: "/dashboard", label: "Trang chính", icon: Home, end: true },
  { to: "/book", label: "Đặt sân", icon: CalendarDays },
  { to: "/bookings", label: "Đơn của tôi", icon: ListChecks },
  { to: "/waitlist", label: "Chờ", icon: Hourglass },
  { to: "/account", label: "Tài khoản", icon: User },
];

export type NavItem = { to: string; label: string; icon: typeof Home; end?: boolean; adminOnly?: boolean };

export const adminItems: NavItem[] = [
  { to: "/admin/club/equipment", label: "Dịch vụ & giải đấu", icon: Dumbbell },
  { to: "/admin/operations", label: "Vận hành", icon: CalendarDays },
  { to: "/admin/contacts", label: "Liên hệ", icon: Inbox, adminOnly: true },
  { to: "/admin", label: "Tổng quan", icon: LayoutDashboard, end: true },
  { to: "/admin/reconciliation", label: "Đối soát", icon: ScrollText },
  { to: "/admin/bookings", label: "Đặt chỗ", icon: ListChecks },
  { to: "/admin/courts", label: "Sân & giá", icon: Dumbbell },
  { to: "/admin/vouchers", label: "Voucher", icon: Ticket },
  { to: "/admin/slots", label: "Khung giờ", icon: Clock },
  { to: "/checkin", label: "Check-in", icon: ScanLine },
  { to: "/admin/branches", label: "Cơ sở", icon: Home, adminOnly: true },
  { to: "/admin/staff", label: "Nhân viên", icon: Users, adminOnly: true },
  { to: "/admin/audit", label: "Nhật ký", icon: ScrollText, adminOnly: true },
  { to: "/admin/membership", label: "Membership", icon: Crown, adminOnly: true },
];

// Subpages share the shell without adding every tool to the primary navigation.
export const experiencePageTitles: Record<string, string> = {
  "/players": "Tìm bạn chơi",
  "/group-expenses": "Chia chi phí",
  "/equipment": "Dụng cụ",
  "/coaches": "Huấn luyện viên",
  "/tournaments": "Giải đấu nhỏ",
  "/calendar-sync": "Lịch tự đồng bộ",
  "/admin/club/equipment": "Kho dụng cụ",
  "/admin/club/coaches": "Quản lý HLV",
  "/admin/club/tournaments": "Quản lý giải",
  "/planner": "Đặt nhiều giờ / định kỳ",
  "/favorites": "Sân yêu thích",
  "/privacy": "Yêu cầu dữ liệu",
  "/media-library": "Thư viện ảnh",
  "/group-meetings": "Buổi chơi nhóm",
  "/admin/operations/calendar": "Lịch vận hành",
  "/admin/operations/cases": "Phản ánh & khiếu nại",
  "/admin/operations/incidents": "Sự cố & liên hệ",
  "/admin/operations/staff-work": "Ca trực & bàn giao",
  "/admin/operations/deliveries": "Trạng thái gửi",
  "/admin/operations/moderation": "Kiểm duyệt đánh giá",
  "/admin/operations/insights": "Khách hàng & báo cáo",
  "/admin/operations/privacy": "Xử lý yêu cầu dữ liệu",
};
