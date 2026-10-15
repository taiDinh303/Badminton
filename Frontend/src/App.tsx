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
import { useAuth, isStaff } from "./store/auth";
import AuthPage from "./pages/AuthPage";
import HomePage from "./pages/HomePage";
import BookPage from "./pages/BookPage";
import MyBookingsPage from "./pages/MyBookingsPage";
import BookingDetailPage from "./pages/BookingDetailPage";
import AccountPage from "./pages/AccountPage";
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
          {
            element: <Staff />,
            children: [
              { path: "admin", element: <AdminDashboardPage /> },
              { path: "admin/bookings", element: <AdminBookingsPage /> },
              { path: "admin/courts", element: <AdminCourtsPage /> },
              { path: "admin/vouchers", element: <AdminVouchersPage /> },
              { path: "admin/slots", element: <AdminSlotsPage /> },
              { path: "admin/staff", element: <AdminStaffPage /> },
              { path: "admin/audit", element: <AdminAuditPage /> },
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
