import { lazy, Suspense } from "react";
import { createBrowserRouter, Navigate, Outlet, useLocation } from "react-router-dom";
import Layout from "../components/Layout";
import { Busy } from "../components/ui";
import { useAuth } from "../store/auth";

const Auth = lazy(() => import("../modules/auth/pages/AuthPage"));
const Home = lazy(() => import("../modules/customer/pages/HomePage"));
const Book = lazy(() => import("../modules/booking/pages/BookPage"));
const Bookings = lazy(() => import("../modules/booking/pages/MyBookingsPage"));
const Detail = lazy(() => import("../modules/booking/pages/BookingDetailPage"));
const Account = lazy(() => import("../modules/account/pages/AccountPage"));
const Waitlist = lazy(() => import("../modules/waitlist/pages/WaitlistPage"));
const Groups = lazy(() => import("../modules/groups/pages/GroupsPage"));
const Reviews = lazy(() => import("../modules/reviews/pages/ReviewsPage"));
const Checkin = lazy(() => import("../modules/checkin/pages/CheckinPage"));
const Dashboard = lazy(() => import("../modules/admin/pages/DashboardPage"));
const AdminBookings = lazy(() => import("../modules/admin/pages/BookingsPage"));
const Courts = lazy(() => import("../modules/admin/pages/CourtsPage"));
const Vouchers = lazy(() => import("../modules/admin/pages/VouchersPage"));
const Slots = lazy(() => import("../modules/admin/pages/SlotsPage"));
const Staff = lazy(() => import("../modules/admin/pages/StaffPage"));
const Audit = lazy(() => import("../modules/admin/pages/AuditPage"));
const Membership = lazy(() => import("../modules/admin/pages/MembershipPage"));

function Gate({ roles }: { roles?: string[] }) {
  const user = useAuth(state => state.user);
  const location = useLocation();
  if (!user) return <Navigate to="/auth" replace state={{ from: location.pathname }} />;
  if (roles && !roles.includes(user.role)) return <Navigate to="/" replace />;
  return <Outlet />;
}

function RouteError() {
  return <main role="alert" className="mx-auto max-w-lg p-8">
    <h1 className="text-xl font-semibold">Không thể tải trang</h1>
    <p className="my-4 text-muted">Vui lòng thử lại. Nếu vẫn gặp lỗi, hãy quay lại sau ít phút.</p>
    <button type="button" className="min-h-11 rounded-lg bg-primary px-4 text-white" onClick={() => window.location.reload()}>Thử lại</button>
  </main>;
}

export const router = createBrowserRouter([
  { path: "/auth", element: <Auth />, errorElement: <RouteError /> },
  { element: <Gate />, errorElement: <RouteError />, children: [{ element: <Layout />, children: [
    { index: true, element: <Home /> },
    { path: "book", element: <Book /> },
    { path: "bookings", element: <Bookings /> },
    { path: "bookings/:id", element: <Detail /> },
    { path: "account", element: <Account /> },
    { path: "waitlist", element: <Waitlist /> },
    { path: "groups", element: <Groups /> },
    { path: "reviews", element: <Reviews /> },
    { element: <Gate roles={["Admin", "Manager"]} />, children: [
      { path: "admin", element: <Dashboard /> },
      { path: "admin/bookings", element: <AdminBookings /> },
      { path: "admin/courts", element: <Courts /> },
      { path: "admin/vouchers", element: <Vouchers /> },
      { path: "admin/slots", element: <Slots /> },
      { path: "checkin", element: <Checkin /> },
      { element: <Gate roles={["Admin"]} />, children: [
        { path: "admin/staff", element: <Staff /> },
        { path: "admin/audit", element: <Audit /> },
        { path: "admin/membership", element: <Membership /> },
      ] },
    ] },
  ] }] },
  { path: "*", element: <Navigate to="/" replace /> },
]);

export function PageLoading() {
  return <div role="status" aria-label="Đang tải trang" className="p-6"><Busy /></div>;
}

export function RouteBoundary({ children }: { children: React.ReactNode }) {
  return <Suspense fallback={<PageLoading />}>{children}</Suspense>;
}
