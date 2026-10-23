import { pageLoaders } from "./pageLoaders";
import { lazy, Suspense } from "react";
import { createBrowserRouter, Navigate, Outlet, useLocation } from "react-router-dom";
import Layout from "../components/Layout";
import { Busy } from "../components/ui";
import { useAuth } from "../store/auth";

const Auth = lazy(pageLoaders.Auth);
const Contacts = lazy(pageLoaders.Contacts);
const Landing = lazy(pageLoaders.Landing);
const Home = lazy(pageLoaders.Home);
const Book = lazy(pageLoaders.Book);
const Bookings = lazy(pageLoaders.Bookings);
const Detail = lazy(pageLoaders.Detail);
const Account = lazy(pageLoaders.Account);
const Waitlist = lazy(pageLoaders.Waitlist);
const Groups = lazy(pageLoaders.Groups);
const Reviews = lazy(pageLoaders.Reviews);
const Checkin = lazy(pageLoaders.Checkin);
const Dashboard = lazy(pageLoaders.Dashboard);
const AdminBookings = lazy(pageLoaders.AdminBookings);
const Courts = lazy(pageLoaders.Courts);
const Vouchers = lazy(pageLoaders.Vouchers);
const Slots = lazy(pageLoaders.Slots);
const Staff = lazy(pageLoaders.Staff);
const Audit = lazy(pageLoaders.Audit);
const Membership = lazy(pageLoaders.Membership);

function Gate({ roles }: { roles?: string[] }) {
  const user = useAuth(state => state.user);
  const location = useLocation();
  if (!user) return <Navigate to="/auth" replace state={{ from: location.pathname + location.search }} />;
  if (roles && !roles.includes(user.role)) return <Navigate to="/dashboard" replace />;
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
  { path: "/", element: <Navigate to="/home" replace /> },
  { path: "/home", element: <Landing />, errorElement: <RouteError /> },
  { path: "/auth", element: <Auth />, errorElement: <RouteError /> },
  { element: <Gate />, errorElement: <RouteError />, children: [{ element: <Layout />, children: [
    { path: "dashboard", element: <Home /> },
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
        { path: "admin/contacts", element: <Contacts /> },
        { path: "admin/staff", element: <Staff /> },
        { path: "admin/audit", element: <Audit /> },
        { path: "admin/membership", element: <Membership /> },
      ] },
    ] },
  ] }] },
  { path: "*", element: <Navigate to="/home" replace /> },
]);

export function PageLoading() {
  return <div role="status" aria-label="Đang tải trang" className="p-6"><Busy /></div>;
}

export function RouteBoundary({ children }: { children: React.ReactNode }) {
  return <Suspense fallback={<PageLoading />}>{children}</Suspense>;
}
