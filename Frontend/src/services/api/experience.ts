import { request } from "../http";
import type { Booking } from "./types";

export interface CalendarEntry {
  id: number;
  code: string;
  courtId: number;
  court: string;
  date: string;
  start: string;
  end: string;
  status: string;
  checkedIn: boolean;
  customer: string;
}
export interface CaseView {
  id: number;
  bookingId: number;
  kind: string;
  reason: string;
  status: string;
  resolution: string | null;
  createdAt: string;
}
export interface HistoryView {
  event: string;
  detail: string;
  at: string;
}
export interface AlternativeSlot {
  courtId: number;
  court: string;
  slotId: number;
  date: string;
  label: string;
  price: number;
}
export interface InboxView {
  id: number;
  bookingId: number | null;
  title: string;
  body: string;
  createdAt: string;
  readAt: string | null;
}
export interface Preferences {
  remindersEnabled: boolean;
  minutesBefore: number;
  email: boolean;
  sms: boolean;
  zalo: boolean;
}
export interface DeliveryView {
  attempts: number;
  id: number;
  bookingId: number | null;
  channel: string;
  status: string;
  error: string | null;
  createdAt: string;
  lastAttempt: string | null;
}
export interface DisruptionView {
  id: number;
  bookingId: number;
  code: string;
  customer: string;
  message: string;
  contactedAt: string | null;
  outcome: string | null;
}
export interface ShiftView {
  id: number;
  branchId: number;
  staffId: number;
  staffName: string;
  startsAtUtc: string;
  endsAtUtc: string;
  note: string;
}
export interface HandoverView {
  id: number;
  branchId: number;
  text: string;
  createdBy: number;
  createdAt: string;
  closedAt: string | null;
  resolution: string | null;
}
export interface PrivacyView {
  id: number;
  userId: number;
  kind: string;
  detail: string;
  status: string;
  resolution: string | null;
  createdAt: string;
}
export interface ReviewReportView {
  id: number;
  reviewId: number;
  reason: string;
  status: string;
  resolution: string | null;
  comment: string | null;
  rating: number;
}
export interface ReviewReplyView {
  id: number;
  reviewId: number;
  text: string;
  createdAt: string;
  updatedAt: string | null;
}
export interface MediaView {
  id: number;
  url: string;
  contentType: string;
  length: number;
}
export interface RsvpView {
  userId: number;
  name: string;
  attending: boolean | null;
}
export interface FavoriteView {
  courtId: number;
  branchId: number;
  courtName: string;
}
export interface CustomerInsights {
  customers: number;
  newCustomers: number;
  returningCustomers: number;
  bookings: number;
  noShows: number;
  noShowRate: number;
  waiting: number;
  promoted: number;
}
export interface BatchRequest {
  expectedTotal?: number;
  courtId: number;
  firstDate: string;
  slotIds: number[];
  weeks: number;
  playerCount: number;
  groupId: number | null;
  requestKey: string;
}

const get = <T>(
  path: string,
  query?: Record<string, string | number | boolean | undefined>,
) => request<T>("GET", path, undefined, query);
const write = <T = string>(method: string, path: string, body?: unknown) =>
  request<T>(method, path, body);
const range = (from: string, to: string, branchId?: number) => ({
  from,
  to,
  branchId,
});

export const experience = {
  operatingCourts: (branchId?: number) =>
    get<FavoriteView[]>("/booking-experience/calendar-courts", { branchId }),
  unreadCount: () => get<number>("/notifications/unread-count"),
  meetings: (groupId: number) =>
    get<
      {
        id: number;
        code: string;
        date: string;
        slot: string;
        ownerId: number;
        playerCount: number;
      }[]
    >(`/community/groups/${groupId}/meetings`),
  findCustomer: (phone: string) =>
    get<{ id: number; name: string; isVerified: boolean } | null>(
      "/booking-experience/desk-customer",
      { phone },
    ),
  shiftStaff: (branchId?: number) =>
    get<{ id: number; name: string; branchId: number | null }[]>(
      "/operations/shift-staff",
      { branchId },
    ),
  batch: (body: BatchRequest) =>
    write<Booking[]>("POST", "/booking-experience/batch", body),
  desk: (body: {
    customerId: number;
    courtId: number;
    slotId: number;
    date: string;
    playerCount: number;
    requestKey: string;
  }) => write<Booking>("POST", "/booking-experience/desk", body),
  reschedule: (
    id: number,
    body: { courtId: number; slotId: number; date: string; reason: string },
  ) => write("POST", `/booking-experience/${id}/reschedule`, body),
  calendar: (from: string, to: string, branchId?: number) =>
    get<CalendarEntry[]>(
      "/booking-experience/calendar",
      range(from, to, branchId),
    ),
  history: (id: number) =>
    get<HistoryView[]>(`/booking-experience/${id}/history`),
  calendarFile: (id: number) =>
    get<string>(`/booking-experience/${id}/calendar-file`),
  alternatives: (courtId: number, date: string, slotId: number) =>
    get<AlternativeSlot[]>("/booking-experience/alternatives", {
      courtId,
      date,
      slotId,
    }),
  cases: (bookingId?: number, branchId?: number, page = 1) =>
    get<CaseView[]>("/booking-experience/cases", { bookingId, branchId, page }),
  openCase: (id: number, kind: string, reason: string) =>
    write<number>("POST", `/booking-experience/${id}/cases`, { kind, reason }),
  resolveCase: (id: number, accepted: boolean, resolution: string) =>
    write("POST", `/booking-experience/cases/${id}/resolve`, {
      accepted,
      resolution,
    }),
  inbox: (page: number) => get<InboxView[]>("/notifications", { page }),
  read: (id: number) => write("PUT", `/notifications/${id}/read`),
  preferences: () => get<Preferences>("/notifications/preferences"),
  savePreferences: (body: Preferences) =>
    write("PUT", "/notifications/preferences", body),
  deliveries: (branchId?: number, page = 1) =>
    get<DeliveryView[]>("/notifications/deliveries", { branchId, page }),
  retryDelivery: (id: number) =>
    write("POST", `/notifications/deliveries/${id}/retry`),
  announce: (body: {
    branchId: number;
    courtId: number | null;
    date: string;
    slotId: number | null;
    message: string;
    requestKey: string;
  }) => write<number>("POST", "/operations/incidents", body),
  disruptions: (branchId?: number, page = 1) =>
    get<DisruptionView[]>("/operations/incidents", { branchId, page }),
  contact: (id: number, outcome: string) =>
    write("PUT", `/operations/incidents/${id}/contact`, { outcome }),
  shifts: (from: string, to: string, branchId?: number) =>
    get<ShiftView[]>("/operations/shifts", range(from, to, branchId)),
  createShift: (body: {
    branchId: number;
    staffId: number;
    startsAtUtc: string;
    endsAtUtc: string;
    note: string;
  }) => write<number>("POST", "/operations/shifts", body),
  deleteShift: (id: number) => write("DELETE", `/operations/shifts/${id}`),
  handovers: (branchId?: number, page = 1) =>
    get<HandoverView[]>("/operations/handover", { branchId, page }),
  handover: (branchId: number, text: string) =>
    write<number>("POST", "/operations/handover", { branchId, text }),
  closeHandover: (id: number, outcome: string) =>
    write("PUT", `/operations/handover/${id}/close`, { outcome }),
  bookingExport: (from: string, to: string, branchId?: number) =>
    get<string>("/operations/booking-export", range(from, to, branchId)),
  auditExport: (from: string, to: string) =>
    get<string>("/operations/audit-export", range(from, to)),
  insights: (from: string, to: string, branchId?: number) =>
    get<CustomerInsights>("/operations/insights", range(from, to, branchId)),
  favorites: () => get<FavoriteView[]>("/community/favorites"),
  favorite: (courtId: number, enabled: boolean) =>
    write("PUT", `/community/favorites/${courtId}`, enabled),
  reportReview: (id: number, reason: string) =>
    write("POST", `/community/reviews/${id}/reports`, { reason }),
  reviewReports: (branchId?: number, page = 1) =>
    get<ReviewReportView[]>("/community/review-reports", { branchId, page }),
  moderateReview: (id: number, hide: boolean, reason: string) =>
    write("PUT", `/community/reviews/${id}/moderation`, { hide, reason }),
  replyReview: (id: number, text: string) =>
    write("PUT", `/community/reviews/${id}/reply`, { text }),
  reviewReply: (id: number) =>
    get<ReviewReplyView | null>(`/community/reviews/${id}/reply`),
  privacy: (page = 1, administration = false) =>
    get<PrivacyView[]>(
      `/community/privacy${administration ? "/administration" : ""}`,
      { page },
    ),
  createPrivacy: (kind: string, detail: string) =>
    write<number>("POST", "/community/privacy", { kind, detail }),
  resolvePrivacy: (id: number, accepted: boolean, resolution: string) =>
    write("PUT", `/community/privacy/${id}/resolve`, { accepted, resolution }),
  rsvps: (id: number) => get<RsvpView[]>(`/booking-experience/${id}/rsvps`),
  invite: (id: number, ids: number[]) =>
    write("POST", `/booking-experience/${id}/invitations`, ids),
  rsvp: (id: number, attending: boolean) =>
    write("PUT", `/booking-experience/${id}/rsvp`, attending),
  avatar: (id: number) => write("PUT", `/media/${id}/avatar`),
  media: (courtId?: number, reviewId?: number) =>
    get<MediaView[]>("/media", { courtId, reviewId }),
  upload: (file: File, courtId?: number, reviewId?: number) => {
    const data = new FormData();
    data.set("file", file);
    if (courtId) data.set("courtId", String(courtId));
    if (reviewId) data.set("reviewId", String(reviewId));
    return write<MediaView>("POST", "/media", data);
  },
  deleteMedia: (id: number) => write("DELETE", `/media/${id}`),
};
