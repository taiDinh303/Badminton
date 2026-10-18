import {
  lazy,
  Suspense,
} from "react";
import {
  Navigate,
  Outlet,
  createBrowserRouter,
  RouterProvider,
} from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useAuth, isStaff, isAdmin } from "./store/auth";
import AuthPage from "./pages/AuthPage";
import HomePage from "./pages/HomePage";
import BookPage from "./pages/BookPage";
import MyBookingsPage from "./pages/MyBookingsPage";
import BookingDetailPage from "./pages/BookingDetailPage";
import AccountPage from "./pages/AccountPage";
import WaitlistPage from "./pages/WaitlistPage";
import GroupsPage from "./pages/GroupsPage";
import ReviewsPage from "./pages/ReviewsPage";
import Layout from "./components/Layout";
import { Busy } from "./components/ui";

// Admin pages — lazy-loaded so the customer bundle stays < 200 KB (docs 07 §6).
const AdminDashboardPage = lazy(() => import("./pages/admin/DashboardPage"));
const AdminBookingsPage = lazy(() => import("./pages/admin/BookingsPage"));
const AdminCourtsPage = lazy(() => import("./pages/admin/CourtsPage"));
const AdminVouchersPage = lazy(() => import("./pages/admin/VouchersPage"));
const AdminSlotsPage = lazy(() => import("./pages/admin/SlotsPage"));
const AdminStaffPage = lazy(() => import("./pages/admin/StaffPage"));
const AdminAuditPage = lazy(() => import("./pages/admin/AuditPage"));
const AdminMembershipPage = lazy(() => import("./pages/admin/MembershipPage"));
const CheckinPage_ = lazy(() => import("./pages/CheckinPage"));

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
});

/** Auth guard: redirect to /auth when no token+user. */
function Protected() {
  const user = useAuth((s) => s.user);
  if (!user) return <Navigate to="/auth" replace />;
  return <Outlet />;
}

/** Staff guard: customers cannot open /admin/*. */
function Staff() {
  const user = useAuth((s) => s.user);
  if (!isStaff(user)) return <Navigate to="/" replace />;
  return (
    <Suspense fallback={<div className="p-4"><Busy /></div>}>
      <Outlet />
    </Suspense>
  );
}

/**
 * Admin-only guard (Manager vs Admin, docs 03 role matrix). The backend
 * enforces [Authorize(Roles="Admin")] on staff/audit/membership — this just
 * keeps the UI honest so a Manager never sees an action that will 403.
 */
function AdminOnly() {
  const user = useAuth((s) => s.user);
  if (!isAdmin(user)) return <Navigate to="/admin" replace />;
  return (
    <Suspense fallback={<div className="p-4"><Busy /></div>}>
      <Outlet />
    </Suspense>
  );
}

const router = createBrowserRouter([
  { path: "/auth", element: <AuthPage /> },
  {
    element: <Protected />,
    children: [
      {
        element: <Layout />,
        children: [
          { index: true, element: <HomePage /> },
          { path: "book", element: <BookPage /> },
          { path: "bookings", element: <MyBookingsPage /> },
          { path: "bookings/:id", element: <BookingDetailPage /> },
          { path: "account", element: <AccountPage /> },
          // FEAT 2 — customer waitlist (protected, not staff-only).
          { path: "waitlist", element: <WaitlistPage /> },
          // FEAT 7/8 — customer groups + public reviews.
          { path: "groups", element: <GroupsPage /> },
          { path: "reviews", element: <ReviewsPage /> },
          {
            element: <Staff />,
            children: [
              { path: "admin", element: <AdminDashboardPage /> },
              { path: "admin/bookings", element: <AdminBookingsPage /> },
              { path: "admin/courts", element: <AdminCourtsPage /> },
              { path: "admin/vouchers", element: <AdminVouchersPage /> },
              { path: "admin/slots", element: <AdminSlotsPage /> },
              // FEAT 9 — staff check-in (Manager/Admin, docs 03 §3).
              { path: "checkin", element: <CheckinPage_ /> },
              // ── Admin-only: backend enforces [Authorize(Roles="Admin")] ──
              {
                element: <AdminOnly />,
                children: [
                  { path: "admin/staff", element: <AdminStaffPage /> },
                  { path: "admin/audit", element: <AdminAuditPage /> },
                  // FEAT 4 — admin membership management.
                  { path: "admin/membership", element: <AdminMembershipPage /> },
                ],
              },
            ],
          },
        ],
      },
    ],
  },
  { path: "*", element: <Navigate to="/" replace /> },
]);

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
}
