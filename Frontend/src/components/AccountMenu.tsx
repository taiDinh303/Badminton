import "./session.css";
import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { ChevronDown, UserRound, CalendarDays, LogOut, House, LayoutDashboard } from "lucide-react";
import { isStaff, useAuth } from "../store/auth";

export default function AccountMenu() {
  const user = useAuth(s => s.user);
  const logout = useAuth(s => s.logout);
  const [open, setOpen] = useState(false);
  const container = useRef<HTMLDivElement>(null);
  const button = useRef<HTMLButtonElement>(null);
  const location = useLocation();
  const navigate = useNavigate();
  useEffect(() => { setOpen(false); }, [location.pathname]);
  useEffect(() => {
    if (!open) return;
    const outside = (event: PointerEvent) => { if (!container.current?.contains(event.target as Node)) setOpen(false); };
    const escape = (event: KeyboardEvent) => { if (event.key === "Escape") { setOpen(false); button.current?.focus(); } };
    document.addEventListener("pointerdown", outside);
    document.addEventListener("keydown", escape);
    return () => { document.removeEventListener("pointerdown", outside); document.removeEventListener("keydown", escape); };
  }, [open]);
  if (!user) return null;
  return <div className="account-menu" ref={container} onBlur={event => { if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false); }}>
    <button ref={button} type="button" className="avatar-trigger" aria-label="Menu tài khoản" aria-expanded={open} aria-controls="account-dropdown" onClick={() => setOpen(!open)}>
      <span className="avatar-circle">{user.fullName.trim().charAt(0).toUpperCase()}</span><span className="avatar-name">{user.fullName}</span><ChevronDown size={15} aria-hidden="true" />
    </button>
    {open && <nav id="account-dropdown" aria-label="Tài khoản" className="account-dropdown">
      <div className="account-identity"><strong>{user.fullName}</strong><span>{user.role === "Admin" ? "Quản trị viên" : isStaff(user) ? "Quản lý" : "Khách hàng"}</span></div>
      <Link to="/dashboard"><LayoutDashboard size={17} />Trang chính</Link>
      <Link to="/account"><UserRound size={17} />Tài khoản</Link>
      <Link to="/bookings"><CalendarDays size={17} />Đơn của tôi</Link>
      <Link to="/home"><House size={17} />Trang giới thiệu</Link>
      <button type="button" onClick={async () => { setOpen(false); await navigate("/home", { replace: true, flushSync: true }); logout(); }}><LogOut size={17} />Đăng xuất</button>
    </nav>}
  </div>;
}
