/**
 * Typed API client for the BadmintonBooking .NET backend.
 *
 * Every payload is wrapped in BaseResponse<T>:
 *   { data, message, statusCode, code }   (camelCase JSON)
 * Errors come back as a non-OK HTTP status + Vietnamese `message`.
 *
 * Base URL (docs 06 §2): in production the SPA points at `VITE_API_URL`
 * (the .NET API behind Cloudflare). In dev we use the same-origin `/api/v1`
 * path, which Vite proxies to the local .NET app.
 */

// Empty/undefined VITE_API_URL → dev same-origin proxy path "/api/v1".
const _apiUrl = (import.meta.env.VITE_API_URL as string | undefined)?.trim();
const BASE = _apiUrl ? _apiUrl.replace(/\/$/, "") : "/api/v1";

export class ApiError extends Error {
  readonly status: number;
  readonly code: string | null;
  constructor(status: number, message: string, code: string | null = null) {
    super(message);
    this.status = status;
    this.code = code;
  }
}

interface BaseResponse<T> {
  data: T;
  message: string | null;
  statusCode: number;
  code: string | null;
}

export const TOKEN_KEY = "bb_token";

function authHeaders(): Record<string, string> {
  const token = localStorage.getItem(TOKEN_KEY);
  return token ? { Authorization: `Bearer ${token}` } : {};
}

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  query?: Record<string, string | number | boolean | undefined | null>,
): Promise<T> {
  const qs = query
    ? `?${new URLSearchParams(
        Object.entries(query)
          .filter(([, v]) => v !== undefined && v !== null)
          .map(([k, v]) => [k, String(v)]),
      ).toString()}`
    : "";
  const res = await fetch(`${BASE}${path}${qs}`, {
    method,
    headers: { "Content-Type": "application/json", ...authHeaders() },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  let json: BaseResponse<unknown> | null = null;
  try {
    json = (await res.json()) as BaseResponse<unknown>;
  } catch {
    /* non-JSON (rare) */
  }

  if (!res.ok) {
    throw new ApiError(res.status, json?.message ?? `HTTP ${res.status}`, json?.code ?? null);
  }
  return json?.data as T;
}

// ── Types (mirror ModelViews records, camelCase) ──────────────────

export interface Branch {
  id: number;
  name: string;
  address: string;
  phone?: string;
  isActive: boolean;
  openTime: string;
  closeTime: string;
  courtCount?: number;
}

export interface Court {
  id: number;
  branchId: number;
  code: string;
  name: string;
  surface: string;
  priceLowPeak: number;
  pricePeak: number;
  weekendSurcharge: number;
  capacity: number;
  isActive: boolean;
  ratingAvg: number | null;
  ratingCount: number;
}

export interface TimeSlot {
  id: number;
  label: string;
  startTime: string;
  endTime: string;
  isPeak: boolean;
  isActive: boolean;
}

export interface Slot {
  slotId: number;
  label: string;
  startTime: string;
  endTime: string;
  isPeak: boolean;
  isLocked: boolean;
  lockReason: string;
}

export interface CourtAvailability {
  courtId: number;
  courtCode: string;
  courtName: string;
  priceLowPeak: number;
  pricePeak: number;
  slots: Slot[];
}

export interface PriceBreakdown {
  basePrice: number;
  weekendSurcharge: number;
  voucherDiscount: number;
  pointsDiscount: number;
  total: number;
  payable: number;
}

export interface Booking {
  id: number;
  bookingCode: string;
  courtId: number;
  courtName: string;
  date: string;
  slotLabel: string;
  playerCount: number;
  status: string;
  totalAmount: number;
  discountAmount: number;
  payableAmount: number;
  pointsEarned: number;
  pointsRedeemed: number;
  groupId: number | null;
  voucherRedemptionId: number | null;
  createdAt: string;
  userName: string | null;
  price: PriceBreakdown;
  cancelReason: string | null;
  /** FEAT check-in: server-generated 64-hex token shown as the QR (docs 03 §3). */
  qrToken?: string | null;
  checkedInAt?: string | null;
  cancelledAt?: string | null;
}

export interface Paged<T> {
  items: T[];
  totalItems: number;
  currentPage: number;
  totalPages: number;
  pageSize: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface LoginResponse {
  userId: number;
  fullName: string;
  phoneNumber: string;
  role: string;
  accessToken: string;
  expiresInMinutes: number;
}

export interface Voucher {
  id: number;
  code: string;
  type: string;
  value: number;
  minSpend: number;
  totalQty: number;
  usedQty: number;
  validFrom: string;
  validUntil: string;
  isActive: boolean;
}

export interface PaymentView {
  id: number;
  bookingId: number;
  method: string;
  amount: number;
  status: string;
  gatewayRef: string | null;
  paidAt: string | null;
  refundAmount: number;
  refundedAt: string | null;
}

export interface Staff {
  id: number;
  fullName: string;
  phoneNumber: string;
  role: string;
  status: string;
  branchId: number | null;
  isVerified: boolean;
  createdAt: string;
}

export interface AuditEntry {
  id: number;
  actorId: number | null;
  actorName: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  beforeJson: string | null;
  afterJson: string | null;
  reason: string | null;
  createdAt: string;
}

export interface ReportSummary {
  todayBookings: number;
  pendingCount: number;
  noShowCount30d: number;
  revenueToday: number;
  peakFillRatePct: number;
  usersToday: number;
  activeCourts: number;
  activeBranches: number;
  /** FEAT 4 — paid members currently active (Silver + Gold, not lapsed). */
  activeMembers?: number;
  /** FEAT 2 — count of customers currently waiting on a full slot. */
  waitingListCount?: number;
}

// ── FEAT 4: Membership V2 ─────────────────────────────────────────────
export type MembershipTier = "None" | "Silver" | "Gold";

export interface MembershipView {
  id: number;
  fullName: string;
  phoneNumber: string;
  tier: MembershipTier;
  expiresAt: string | null;
  lapsed: boolean;
  multiplier: number;
}

// ── FEAT 2: Waitlist ───────────────────────────────────────────────────
export interface WaitlistEntryView {
  id: number;
  courtId: number;
  courtName: string;
  slotLabel: string;
  date: string;
  playerCount: number;
  status: "Waiting" | "Promoted" | "Declined" | "Cancelled" | "Expired";
  createdAt: string;
  promotedBookingId: number | null;
}

// ── FEAT 7: Groups (docs 03 §7) ───────────────────────────────────────
export interface GroupView {
  id: number;
  name: string;
  ownerId: number;
  ownerName: string;
  maxMembers: number;
  memberCount: number;
  createdAt: string;
}

export interface GroupMemberView {
  userId: number;
  fullName: string;
  phoneNumber: string;
  isCreator: boolean;
  joinedAt: string;
}

// ── FEAT 8: Reviews (docs 03 §7) ──────────────────────────────────────
export interface ReviewView {
  id: number;
  bookingId: number;
  courtId: number;
  courtName: string;
  userName: string;
  rating: number;
  comment: string | null;
  images: string[] | null;
  createdAt: string;
  flaggedRefunded: boolean;
}

// ── FEAT 9: Staff check-in (docs 03 §3) ───────────────────────────────
export interface SlotCheckinView {
  id: number;
  bookingCode: string;
  status: string;
  checkedInAt: string | null;
}

export interface RevenueByDay {
  date: string;
  bookings: number;
  revenue: number;
  refunds: number;
}

export interface OccupancyByDay {
  date: string;
  branchId: number | null;
  branchName: string | null;
  bookedSlots: number;
  totalSlots: number;
  fillRatePct: number;
  peakBooked: number;
  peakTotal: number;
  peakFillRatePct: number;
}

// ── Endpoints ───────────────────────────────────────────────────────

export const api = {
  auth: {
    register: (b: {
      fullName: string;
      phoneNumber: string;
      password: string;
      email?: string;
      role?: "Customer" | "Manager" | "Admin";
    }) => request<LoginResponse>("POST", "/auth/register", b),
    login: (b: { phoneNumber: string; password: string; email?: string }) =>
      request<LoginResponse>("POST", "/auth/login", b),
  },
  branches: {
    list: () => request<Branch[]>("GET", "/branches"),
    get: (id: number) => request<Branch>("GET", `/branches/${id}`),
    create: (b: {
      name: string;
      address: string;
      geoLat?: number;
      geoLng?: number;
      openTime?: string;
      closeTime?: string;
    }) => request<number>("POST", "/branches", b),
    update: (
      id: number,
      b: {
        name?: string;
        address?: string;
        isActive?: boolean;
        openTime?: string;
        closeTime?: string;
      },
    ) => request<unknown>("PATCH", `/branches/${id}`, b),
    delete: (id: number) => request<unknown>("DELETE", `/branches/${id}`),
  },
  courts: {
    list: (branchId?: number) =>
      request<Court[]>("GET", "/courts", undefined, { branchId }),
    create: (b: {
      branchId: number;
      code: string;
      name: string;
      priceLowPeak: number;
      pricePeak: number;
      weekendSurcharge?: number;
      capacity?: number;
      surface?: string;
      amenities?: string | null;
    }) => request<number>("POST", "/courts", b),
    update: (
      id: number,
      b: {
        name?: string;
        priceLowPeak?: number;
        pricePeak?: number;
        weekendSurcharge?: number;
        capacity?: number;
        surface?: string;
        amenities?: string | null;
        isActive?: boolean;
      },
    ) => request<unknown>("PATCH", `/courts/${id}`, b),
    delete: (id: number) => request<unknown>("DELETE", `/courts/${id}`),
  },
  timeSlots: {
    list: () => request<TimeSlot[]>("GET", "/time-slots"),
    create: (b: {
      label: string;
      startTime: string;
      endTime: string;
      isPeak: boolean;
    }) => request<number>("POST", "/time-slots", b),
    update: (id: number, b: { isActive?: boolean; label?: string }) =>
      request<unknown>("PATCH", `/time-slots/${id}`, b),
    delete: (id: number) => request<unknown>("DELETE", `/time-slots/${id}`),
  },
  booking: {
    availability: (branchId: number, date: string) =>
      request<CourtAvailability[]>(
        "GET",
        `/bookings/availability/${branchId}/${date}`,
      ),
    create: (b: {
      courtId: number;
      date: string;
      slotId: number;
      playerCount?: number;
      groupId?: number | null;
      voucherCode?: string | null;
      idempotencyKey?: string | null;
      note?: string | null;
      pointsToRedeem?: number | null;
    }) => request<Booking>("POST", "/bookings", b),
    mine: (status?: string, page = 1, pageSize = 50) =>
      request<Paged<Booking>>("GET", "/bookings/mine", undefined, {
        status,
        page,
        pageSize,
      }),
    getById: (id: number) => request<Booking>("GET", `/bookings/${id}`),
    cancel: (id: number) => request<Booking>("POST", `/bookings/${id}/cancel`),
    // Admin: list all bookings (filterable), paged.
    all: (
      status?: string,
      branchId?: number,
      page = 1,
      pageSize = 100,
    ) =>
      request<Paged<Booking>>("GET", "/bookings/all", undefined, {
        status,
        branchId,
        page,
        pageSize,
      }),
    // Staff (Manager/Admin) lifecycle:
    complete: (id: number, note?: string) =>
      request<Booking>("POST", `/bookings/${id}/complete`, undefined, { note }),
    noShow: (id: number, note?: string) =>
      request<Booking>("POST", `/bookings/${id}/no-show`, undefined, { note }),
  },
  payments: {
    init: (b: { bookingId: number; method: string; returnUrl?: string }) =>
      request<{ method: string; gatewayUrl: string | null; gatewayRef: string | null; bookingCode: string }>(
        "POST",
        "/payments/init",
        b,
      ),
    onSite: (bookingId: number, method = "cash", note?: string) =>
      request<unknown>("POST", `/payments/${bookingId}/on-site`, undefined, {
        method,
        note,
      }),
    refund: (bookingId: number, reason?: string) =>
      request<unknown>("POST", `/payments/${bookingId}/refund`, undefined, {
        reason,
      }),
    forBooking: (bookingId: number) =>
      request<PaymentView[]>("GET", `/payments/booking/${bookingId}`),
  },
  vouchers: {
    preview: (code: string, baseAmount: number) =>
      request<{ discount: number; total: number }>(
        "GET",
        `/vouchers/${encodeURIComponent(code)}/preview`,
        undefined,
        { baseAmount },
      ),
    get: (code: string) => request<Voucher>("GET", `/vouchers/${encodeURIComponent(code)}`),
    list: (activeOnly = true) =>
      request<Voucher[]>("GET", "/vouchers", undefined, { activeOnly }),
    create: (b: {
      code: string;
      type: "Percent" | "Fixed";
      value: number;
      maxValue?: number | null;
      minSpend?: number;
      maxPerUser?: number;
      totalQty?: number;
      validFrom?: string;
      validUntil?: string;
    }) => request<unknown>("POST", "/vouchers", b),
    deactivate: (id: number) =>
      request<unknown>("POST", `/vouchers/${id}/deactivate`),
  },
  // ── Staff / admin (Manager + Admin, docs 07 §3) ──────────────
  admin: {
    staff: {
      list: () => request<Staff[]>("GET", "/admin/staff"),
      create: (b: {
        fullName: string;
        phoneNumber: string;
        password: string;
        email?: string;
        role?: "Manager" | "Admin";
        branchId?: number;
      }) => request<number>("POST", "/admin/staff", b),
      lock: (id: number) => request<unknown>("POST", `/admin/staff/${id}/lock`),
      unlock: (id: number) =>
        request<unknown>("POST", `/admin/staff/${id}/unlock`),
      changeRole: (id: number, role: string) =>
        request<unknown>("PATCH", `/admin/staff/${id}/role`, undefined, { role }),
      assignBranch: (id: number, branchId: number) =>
        request<unknown>(
          "PATCH",
          `/admin/staff/${id}/branch`,
          undefined,
          { branchId },
        ),
    },
    audit: {
      query: (q: {
        entityType?: string;
        entityId?: string;
        from?: string;
        to?: string;
        page?: number;
        pageSize?: number;
      }) =>
        request<Paged<AuditEntry>>("GET", "/admin/audit", undefined, {
          entityType: q.entityType,
          entityId: q.entityId,
          from: q.from,
          to: q.to,
          page: q.page,
          pageSize: q.pageSize,
        }),
    },
    maintenance: {
      set: (
        courtId: number,
        date: string,
        status: "Open" | "Maintenance" | "Closed",
        note?: string,
      ) =>
        request<unknown>(
          "POST",
          `/admin/courts/${courtId}/maintenance`,
          undefined,
          { date, status, note },
        ),
      get: (courtId: number, date: string) =>
        request<unknown>(
          "GET",
          `/admin/courts/${courtId}/maintenance/${date}`,
        ),
    },
    report: {
      summary: (branchId?: number) =>
        request<ReportSummary>(
          "GET",
          "/admin/report/summary",
          undefined,
          { branchId },
        ),
      revenue: (from: string, to: string, branchId?: number) =>
        request<RevenueByDay[]>(
          "GET",
          "/admin/report/revenue",
          undefined,
          { from, to, branchId },
        ),
      occupancy: (from: string, to: string, branchId?: number) =>
        request<OccupancyByDay[]>(
          "GET",
          "/admin/report/occupancy",
          undefined,
          { from, to, branchId },
        ),
    },
    // ── FEAT 4: Membership V2 (admin assigns paid tiers) ───────────
    membership: {
      assign: (userId: number, tier: MembershipTier) =>
        request<unknown>("POST", "/admin/membership/assign", { userId, tier }),
      members: () =>
        request<MembershipView[]>("GET", "/admin/membership/members"),
    },
  },

  // ── FEAT 2: Waitlist (customer joins a full slot's queue) ─────────
  waitlist: {
    join: (b: {
      courtId: number;
      slotId: number;
      date: string;
      playerCount?: number;
      note?: string | null;
    }) => request<unknown>("POST", "/waitlist/join", b),
    leave: (id: number) => request<unknown>("POST", `/waitlist/${id}/leave`),
    mine: () => request<WaitlistEntryView[]>("GET", "/waitlist/mine"),
  },

  // ── FEAT 7: Groups (docs 03 §7) ───────────────────────────────────
  groups: {
    mine: () => request<GroupView[]>("GET", "/groups/mine"),
    create: (name: string, maxMembers = 10) =>
      request<GroupView>("POST", "/groups", { name, maxMembers }),
    join: (id: number) => request<unknown>("POST", `/groups/${id}/join`),
    members: (id: number) =>
      request<GroupMemberView[]>("GET", `/groups/${id}/members`),
  },

  // ── FEAT 8: Reviews (docs 03 §7) ──────────────────────────────────
  reviews: {
    forCourt: (courtId: number) =>
      request<ReviewView[]>("GET", "/reviews", undefined, { courtId }),
    create: (b: { bookingId: number; rating: number; comment?: string | null }) =>
      request<ReviewView>("POST", "/reviews", b),
  },

  // ── FEAT 9: Staff check-in (docs 03 §3) ───────────────────────────
  checkin: {
    byCode: (code: string) =>
      request<{ BookingId: number; Message: string }>(
        "POST",
        `/checkin/code/${encodeURIComponent(code)}`,
      ),
    byQr: (token: string) =>
      request<{ BookingId: number; Message: string }>(
        "POST",
        `/checkin/qr/${encodeURIComponent(token)}`,
      ),
    slot: (courtId: number, date: string, slotLabel: string) =>
      request<SlotCheckinView[]>(
        "GET",
        `/checkin/slot/${courtId}/${date}/${encodeURIComponent(slotLabel)}`,
      ),
  },
};

// ── Formatting helpers (docs 07: ngày dd/MM/yyyy, tiền #.##0đ) ────

export function formatVnd(n: number): string {
  return `${Math.round(n).toLocaleString("vi-VN")}đ`;
}

export function formatDay(date: string): string {
  // Backend is mixed: BookingView.Date & membership are "dd/MM/yyyy" (Vietnamian
  // day-first), report dates are "yyyy-MM-dd", some are ISO datetimes.
  // Handle all three and always return dd/MM/yyyy.
  if (!date) return "—";
  const s = String(date).trim();
  // Already day-first (dd/MM/yyyy or d/M/yyyy) → pass through.
  if (/^\d{1,2}\/\d{1,2}\/\d{2,4}$/.test(s)) return s;
  // yyyy-MM-dd or ISO datetime → split on '-'.
  if (s.includes("-")) {
    const [y, m, d] = s.split("-");
    if (y && m && d) return `${d}/${m}/${y}`;
  }
  // ISO datetime fallback.
  const dt = new Date(s);
  if (!Number.isNaN(dt.getTime())) {
    return `${String(dt.getDate()).padStart(2, "0")}/${String(dt.getMonth() + 1).padStart(2, "0")}/${dt.getFullYear()}`;
  }
  return s;
}

/** Backend slotLabel = "14:00 (02:00-03:00)" — the parenthetical is a
 *  12-hour time that confuses users. Keep just the label portion ("14:00"). */
export function slotLabelClean(label: string | null | undefined): string {
  if (!label) return "—";
  return label.includes("(") ? label.split("(")[0].trim() : label;
}
