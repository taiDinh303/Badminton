import { lazy } from "react";
const Players = lazy(() => import("../modules/club/pages/PlayersPage"));
const Expenses = lazy(() => import("../modules/club/pages/ExpensesPage"));
const Equipment = lazy(() => import("../modules/club/pages/EquipmentPage"));
const Coaches = lazy(() => import("../modules/club/pages/CoachesPage"));
const Tournaments = lazy(() => import("../modules/club/pages/TournamentsPage"));
const CalendarSync = lazy(
  () => import("../modules/club/pages/CalendarSyncPage"),
);
export const clubCustomerRoutes = [
  { path: "players", element: <Players /> },
  { path: "group-expenses", element: <Expenses /> },
  { path: "equipment", element: <Equipment /> },
  { path: "coaches", element: <Coaches /> },
  { path: "tournaments", element: <Tournaments /> },
  { path: "calendar-sync", element: <CalendarSync /> },
];
export const clubStaffRoutes = [
  { path: "admin/club/equipment", element: <Equipment staff /> },
  { path: "admin/club/coaches", element: <Coaches staff /> },
  { path: "admin/club/tournaments", element: <Tournaments staff /> },
];
