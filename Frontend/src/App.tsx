import {
  Navigate,
  Outlet,
  createBrowserRouter,
  RouterProvider,
} from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useAuth } from "./store/auth";
import AuthPage from "./pages/AuthPage";
import HomePage from "./pages/HomePage";
import BookPage from "./pages/BookPage";
import MyBookingsPage from "./pages/MyBookingsPage";
import BookingDetailPage from "./pages/BookingDetailPage";
import AccountPage from "./pages/AccountPage";
import Layout from "./components/Layout";

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
