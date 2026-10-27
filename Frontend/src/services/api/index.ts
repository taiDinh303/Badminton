import { request } from "../http";
import type { BookingRequest, PriceBreakdown, Branch, Court, TimeSlot, CourtAvailability, Booking, Paged, LoginResponse, Voucher, PaymentView, Staff, AuditEntry, ReportSummary, MembershipTier, MembershipView, WaitlistEntryView, GroupView, GroupMemberView, ReviewView, SlotCheckinView, RevenueByDay, OccupancyByDay, PointSummary, MyVoucher, AccountSummary, ProfileView } from "./types";
export type * from "./types";
export { ApiError, TOKEN_KEY } from "../http";

export const api = {
  auth: {
    resetPassword: (b: { phoneNumber: string; code: string; newPassword: string }) => request<string>("POST", "/auth/reset-password", b),
    recoveryCode: (id: number, b: { currentPassword: string; reason: string }) => request<{ code: string; expiresAt: string }>("POST", `/auth/recovery-code/${id}`, b),
    changePassword: (b: { currentPassword: string; newPassword: string }) => request<string>("POST", "/auth/change-password", b),
    revokeSessions: () => request<string>("POST", "/auth/logout"),
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
    impact: (id: number) => request<{ count: number; bookingCodes: string[] }>("GET", `/branches/${id}/impact`),
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
        geoLat?: number | null;
        geoLng?: number | null;
        hasGeo?: boolean;
      },
    ) => request<unknown>("PATCH", `/branches/${id}`, b),
    delete: (id: number) => request<unknown>("DELETE", `/branches/${id}`),
  },
  courts: {
    impact: (id: number) => request<{ count: number; bookingCodes: string[] }>("GET", `/courts/${id}/impact`),
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
    list: (includeInactive = false) => request<TimeSlot[]>("GET", "/time-slots", undefined, { includeInactive }),
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
    quote: (b: BookingRequest) => request<PriceBreakdown>("POST", "/bookings/quote", b),
    create: (b: BookingRequest) => request<Booking>("POST", "/bookings", b),
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
      search?: string, date?: string, needsAttendance?: boolean,
    ) =>
      request<Paged<Booking>>("GET", "/bookings/all", undefined, {
        status,
        branchId,
        page,
        pageSize, search, date, needsAttendance,
      }),
    // Staff (Manager/Admin) lifecycle:
    complete: (id: number, note?: string) =>
      request<Booking>("POST", `/bookings/${id}/complete`, undefined, { note }),
    noShow: (id: number, note?: string) =>
      request<Booking>("POST", `/bookings/${id}/no-show`, undefined, { note }),
  },
  payments: {
    reconcileRefund: (paymentId: number) => request<PaymentView>("POST", `/payments/reconciliation/${paymentId}/refund`),
    reconciliation: (page = 1) => request<{ items: { payment: PaymentView; reason: string | null; createdAt: string }[]; totalCount: number }>("GET", "/payments/reconciliation", undefined, { page, pageSize: 50 }),
    methods: () => request<string[]>("GET", "/payments/methods"),
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
      request<number>(
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
  // ── Staff / admin (Manager + Admin) ──────────────
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
      schedule: (courtId: number, date: string) => request<{ status: "Open" | "Maintenance" | "Closed"; startTime: string | null; endTime: string | null; note: string | null }>("GET", `/admin/courts/${courtId}/maintenance/${date}/schedule`),
      set: (
        courtId: number,
        date: string,
        status: "Open" | "Maintenance" | "Closed",
        note?: string,
        startTime?: string,
        endTime?: string,
      ) =>
        request<unknown>(
          "POST",
          `/admin/courts/${courtId}/maintenance`,
          undefined,
          { date, status, note, startTime, endTime },
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
      customers: () => request<MembershipView[]>("GET", "/admin/membership/customers"),
      assign: (userId: number, tier: MembershipTier) =>
        request<unknown>("POST", "/admin/membership/assign", { userId, tier }),
      members: () =>
        request<MembershipView[]>("GET", "/admin/membership/members"),
    },
  },

  // ── FEAT 2: Waitlist (customer joins a full slot's queue) ─────────
  waitlist: {
    paged: (page = 1) => request<Paged<WaitlistEntryView>>("GET", "/waitlist/mine/paged", undefined, { page }),
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

  // ── FEAT 7: Groups ───────────────────────────────────
  groups: {
    mine: () => request<GroupView[]>("GET", "/groups/mine"),
    create: (name: string, maxMembers = 10) =>
      request<GroupView>("POST", "/groups", { name, maxMembers }),
    join: (id: number, inviteCode?: string) => request<unknown>("POST", `/groups/${id}/join`, { inviteCode }),
    joined: () => request<GroupView[]>("GET", "/groups/joined"),
    leave: (id: number) => request<unknown>("POST", `/groups/${id}/leave`),
    rename: (id: number, name: string) => request<unknown>("PATCH", `/groups/${id}`, { name }),
    disband: (id: number) => request<unknown>("DELETE", `/groups/${id}`),
    remove: (id: number, userId: number) => request<unknown>("DELETE", `/groups/${id}/members/${userId}`),
    transfer: (id: number, userId: number) => request<unknown>("POST", `/groups/${id}/transfer`, { userId }),
    invite: (id: number) => request<{ code: string }>("POST", `/groups/${id}/invite`),
    members: (id: number) =>
      request<GroupMemberView[]>("GET", `/groups/${id}/members`),
  },

  // ── FEAT 8: Reviews ──────────────────────────────────
  reviews: {
    forBooking: (id: number) => request<ReviewView | null>("GET", `/reviews/booking/${id}`),
    paged: (courtId: number | null, page = 1) => request<Paged<ReviewView> & { averageRating: number | null }>("GET", "/reviews/paged", undefined, { courtId, page, pageSize: 20 }),
    forCourt: (courtId: number) =>
      request<ReviewView[]>("GET", "/reviews", undefined, { courtId }),
    create: (b: { bookingId: number; rating: number; comment?: string | null }) =>
      request<ReviewView>("POST", "/reviews", b),
  },

  // ── FEAT 9: Staff check-in ───────────────────────────
  checkin: {
    byCode: (code: string) =>
      request<{ bookingId: number; message: string }>(
        "POST",
        `/checkin/code/${encodeURIComponent(code)}`,
      ),
    byQr: (token: string) =>
      request<{ bookingId: number; message: string }>(
        "POST",
        "/checkin/token",
        { token },
      ),
    slot: (courtId: number, date: string, slotLabel: string) =>
      request<SlotCheckinView[]>(
        "GET",
        `/checkin/slot/${courtId}/${date}/${encodeURIComponent(slotLabel)}`,
      ),
  },

  // ── Account summary: points + voucher usage ──────────
  account: {
    profile: () => request<ProfileView>("GET", "/account/profile"),
    update: (b: { fullName: string; email: string | null; avatarUrl: string | null }) => request<ProfileView>("PATCH", "/account/profile", b),
    points: () => request<PointSummary>("GET", "/account/points"),
    vouchers: () => request<MyVoucher[]>("GET", "/account/vouchers"),
    summary: () => request<AccountSummary>("GET", "/account/summary"),
  },
};
