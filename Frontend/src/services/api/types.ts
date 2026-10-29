export interface Branch {
  id: number;
  name: string;
  address: string;
  phone?: string;
  isActive: boolean;
  openTime: string;
  closeTime: string;
  courtCount?: number;
  geoLat?: number | null;
  geoLng?: number | null;
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

export interface BookingRequest {
  courtId: number; date: string; slotId: number; playerCount?: number;
  groupId?: number | null; voucherCode?: string | null;
  idempotencyKey?: string | null; note?: string | null; pointsToRedeem?: number | null;
}

export interface CourtAvailability {
  capacity?: number;
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
  userId?: number;
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
  /** FEAT check-in: server-generated 64-hex token shown as the QR. */
  qrToken?: string | null;
  checkedInAt?: string | null;
  pendingExpiresAt?: string | null;
  serverTimeUtc?: string | null;
  slotStartAtUtc?: string | null;
  slotEndAtUtc?: string | null;
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
  attendanceToReview?: number;
  todayBookings: number;
  pendingCount: number;
  noShowCount30d: number;
  revenueToday: number;
  peakFillRatePct: number;
  usersToday: number;
  activeCourts: number;
  activeBranches: number;
  /** FEAT 4 â€” paid members currently active (Silver + Gold, not lapsed). */
  activeMembers?: number;
  /** FEAT 2 â€” count of customers currently waiting on a full slot. */
  waitingListCount?: number;
  // NOTE: RevenueByDay / OccupancyByDay live at the bottom of this file.
}

// â”€â”€ FEAT 4: Membership V2 â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

// â”€â”€ FEAT 2: Waitlist â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
export interface WaitlistEntryView {
  declineReason?: string | null;
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

// â”€â”€ FEAT 7: Groups â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
export interface GroupView {
  inviteCode?: string | null;
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

// â”€â”€ FEAT 8: Reviews â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

// â”€â”€ FEAT 9: Staff check-in â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
export interface SlotCheckinView {
  id: number;
  bookingCode: string;
  status: string;
  checkedInAt: string | null;
}

// â”€â”€ Account summary: loyalty points + voucher usage â”€â”€â”€â”€â”€â”€
export interface PointSummary {
  balance: number;
  redeemableVnd: number;
  vndPer1000Points: number;
  pointsPerVndUnit: number;
}

export interface MyVoucher {
  id: number;
  code: string;
  type: string;
  value: number;
  maxValue: number | null;
  minSpend: number;
  alreadyUsed: boolean;
  validFrom: string | null;
  validUntil: string | null;
  isActive: boolean;
}

export interface AccountSummary {
  legacyPointsConverted?: boolean;
  pointsConvertedAt?: string | null;
  pointsBalance: number;
  pointsRedeemableVnd: number;
  vndPer1000Points: number;
  pointsPerVndUnit: number;
  voucherUsed: boolean;
  usedVoucher: MyVoucher | null;
  nextVoucher: MyVoucher | null;
  availableVouchers: MyVoucher[];
}

// â”€â”€ Admin report views â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
export interface RevenueByDay {
  date: string;
  bookings: number;
  revenue: number;
  refunds: number;
}

export interface OccupancyByDay {
  historicalEstimate?: boolean;
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

export interface ProfileView { id: number; fullName: string; phoneNumber: string; email: string | null; avatarUrl: string | null; tier: MembershipTier; membershipExpiresAt: string | null; multiplier: number; }
