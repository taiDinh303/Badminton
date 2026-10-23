import { Home, CalendarDays, ListChecks, User, LayoutDashboard, Dumbbell, Ticket, Clock, Users, ScrollText, Crown, Hourglass, Star, ScanLine } from "lucide-react";

export const customerItems = [
  { to: "/home", label: "Trang chủ", icon: Home, end: true },
  { to: "/book", label: "Đặt sân", icon: CalendarDays },
  { to: "/bookings", label: "Đơn của tôi", icon: ListChecks },
  { to: "/waitlist", label: "Chờ", icon: Hourglass },
  { to: "/groups", label: "Nhóm", icon: Users },
  { to: "/reviews", label: "Đánh giá", icon: Star },
  { to: "/account", label: "Tài khoản", icon: User },
];

// Mobile bottom nav keeps only the essentials so 5 items fit (docs 07 §3).
export const mobileItems = [
  { to: "/home", label: "Trang chủ", icon: Home, end: true },
  { to: "/book", label: "Đặt sân", icon: CalendarDays },
  { to: "/bookings", label: "Đơn của tôi", icon: ListChecks },
  { to: "/waitlist", label: "Chờ", icon: Hourglass },
  { to: "/account", label: "Tài khoản", icon: User },
];

export type NavItem = { to: string; label: string; icon: typeof Home; end?: boolean; adminOnly?: boolean };

// Docs 03 role matrix: a Manager handles day-to-day ops (check-in, bookings,
// maintenance on own branch, slots, vouchers, dashboard-summary). Admin adds
// the back-office: staff, audit log, membership, court CRUD.
export const adminItems: NavItem[] = [
  { to: "/admin", label: "Tổng quan", icon: LayoutDashboard, end: true },
  { to: "/admin/bookings", label: "Đặt chỗ", icon: ListChecks },
  { to: "/admin/courts", label: "Sân & giá", icon: Dumbbell },
  { to: "/admin/vouchers", label: "Voucher", icon: Ticket },
  { to: "/admin/slots", label: "Khung giờ", icon: Clock },
  { to: "/checkin", label: "Check-in", icon: ScanLine },
  { to: "/admin/staff", label: "Nhân viên", icon: Users, adminOnly: true },
  { to: "/admin/audit", label: "Nhật ký", icon: ScrollText, adminOnly: true },
  { to: "/admin/membership", label: "Membership", icon: Crown, adminOnly: true },
];
