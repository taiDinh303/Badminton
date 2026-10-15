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
  },
};

// ── Formatting helpers (docs 07: ngày dd/MM/yyyy, tiền #.##0đ) ────

export function formatVnd(n: number): string {
  return `${Math.round(n).toLocaleString("vi-VN")}đ`;
}

export function formatDay(date: string): string {
  // Backend sends DateOnly as "yyyy-MM-dd".
  const [y, m, d] = date.split("-");
  return `${d}/${m}/${y}`;
}
