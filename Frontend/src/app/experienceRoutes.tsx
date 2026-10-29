import { lazy } from "react";

const Planner = lazy(() => import("../modules/experience/pages/PlannerPage"));
const Notifications = lazy(
  () => import("../modules/experience/pages/NotificationsPage"),
);
const Favorites = lazy(
  () => import("../modules/experience/pages/FavoritesPage"),
);
const Privacy = lazy(() => import("../modules/experience/pages/PrivacyPage"));
const Media = lazy(
  () => import("../modules/experience/pages/MediaLibraryPage"),
);
const Meetings = lazy(
  () => import("../modules/experience/pages/GroupMeetingsPage"),
);
const Hub = lazy(() => import("../modules/experience/pages/OperationsHubPage"));
const Calendar = lazy(
  () => import("../modules/experience/pages/OperationsCalendarPage"),
);
const Cases = lazy(
  () => import("../modules/experience/pages/BookingCasesPage"),
);
const Incidents = lazy(
  () => import("../modules/experience/pages/DisruptionsPage"),
);
const StaffWork = lazy(
  () => import("../modules/experience/pages/StaffWorkPage"),
);
const Deliveries = lazy(
  () => import("../modules/experience/pages/DeliveriesPage"),
);
const Moderation = lazy(
  () => import("../modules/experience/pages/ModerationPage"),
);
const Insights = lazy(() => import("../modules/experience/pages/InsightsPage"));

export const experienceCustomerRoutes = [
  { path: "planner", element: <Planner /> },
  { path: "notifications", element: <Notifications /> },
  { path: "favorites", element: <Favorites /> },
  { path: "privacy", element: <Privacy /> },
  { path: "media-library", element: <Media /> },
  { path: "group-meetings", element: <Meetings /> },
];
export const experienceStaffRoutes = [
  { path: "admin/operations", element: <Hub /> },
  { path: "admin/operations/calendar", element: <Calendar /> },
  { path: "admin/operations/cases", element: <Cases /> },
  { path: "admin/operations/incidents", element: <Incidents /> },
  { path: "admin/operations/staff-work", element: <StaffWork /> },
  { path: "admin/operations/deliveries", element: <Deliveries /> },
  { path: "admin/operations/moderation", element: <Moderation /> },
  { path: "admin/operations/insights", element: <Insights /> },
];
export const experienceAdminRoutes = [
  { path: "admin/operations/privacy", element: <Privacy administration /> },
];
