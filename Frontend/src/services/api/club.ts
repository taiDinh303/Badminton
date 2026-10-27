import { request } from "../http";
import { apiBase } from "../../../config/environment.mjs";
export interface PlayerSession {
  id: number;
  ownerId: number;
  owner: string;
  branchId: number;
  area: string;
  level: number;
  startsAtUtc: string;
  endsAtUtc: string;
  capacity: number;
  accepted: number;
  note: string;
  closed: boolean;
  myStatus: string | null;
}
export interface SessionMember {
  userId: number;
  name: string;
  status: string;
}
export interface Expense {
  splitMethod?: string;
  id: number;
  bookingId: number;
  total: number;
  bookingCancelled: boolean;
  ownerId: number;
  shares: {
    userId: number;
    name: string;
    amount: number;
    paidAt: string | null;
    attendedMinutes?: number | null;
  }[];
}
export interface Equipment {
  id: number;
  branchId: number;
  name: string;
  rental: boolean;
  stock: number;
  available: number;
  unitPrice: number;
  active: boolean;
}
export interface EquipmentOrder {
  overdue?: boolean;
  id: number;
  userId: number;
  branchId: number;
  item: string;
  rental: boolean;
  quantity: number;
  total: number;
  status: string;
  dueAtUtc: string;
  returnedAtUtc: string | null;
}
export interface Coach {
  id: number;
  branchId: number;
  name: string;
  bio: string;
  active: boolean;
}
export interface CoachSlot {
  courtBookingId?: number | null;
  courtReady?: boolean;
  id: number;
  coachId: number;
  startsAtUtc: string;
  endsAtUtc: string;
  capacity: number;
  available: number;
  pricePerPerson: number;
  cancelled: boolean;
}
export interface Coaching {
  id: number;
  userId: number;
  branchId: number;
  coach: string;
  startsAtUtc: string;
  endsAtUtc: string;
  people: number;
  total: number;
  status: string;
}
export interface Tournament {
  id: number;
  branchId: number;
  organizerId: number;
  name: string;
  maxTeams: number;
  status: string;
}
export interface Team {
  id: number;
  captainId: number;
  name: string;
  players: string;
  pool: number;
  played: number;
  won: number;
  pointsFor: number;
  pointsAgainst: number;
}
export interface Match {
  id: number;
  teamAId: number;
  teamBId: number;
  pool: number;
  round: number;
  startsAtUtc: string | null;
  endsAtUtc: string | null;
  location: string;
  scoreA: number | null;
  scoreB: number | null;
}
export interface TournamentDetail {
  tournament: Tournament;
  teams: Team[];
  matches: Match[];
}
const base = "/club";
export const club = {
  splitAttendance: (
    id: number,
    members: { userId: number; minutes: number }[],
  ) => request("POST", `${base}/expenses/${id}/attendance`, { members }),
  condition: (id: number) =>
    request<{
      actorId: number;
      unusableQuantity: number;
      lost: boolean;
      assessedAmount: number;
      reason: string;
      at: string;
    } | null>("GET", `${base}/equipment/orders/${id}/condition`),
  recordCondition: (
    id: number,
    r: {
      unusableQuantity: number;
      lost: boolean;
      assessedAmount: number;
      reason: string;
    },
  ) => request("POST", `${base}/equipment/orders/${id}/condition`, r),
  courtBookings: (branchId: number) =>
    request<
      {
        bookingId: number;
        code: string;
        court: string;
        startsAtUtc: string;
        endsAtUtc: string;
      }[]
    >("GET", `${base}/coaches/court-bookings`, undefined, { branchId }),
  attachCourt: (id: number, bookingId: number) =>
    request("PUT", `${base}/coaches/slots/${id}/court`, bookingId),
  scoreHistory: (id: number, matchId: number) =>
    request<
      {
        revision: number;
        actorId: number;
        oldScoreA: number | null;
        oldScoreB: number | null;
        scoreA: number;
        scoreB: number;
        reason: string;
        at: string;
      }[]
    >("GET", `${base}/tournaments/${id}/matches/${matchId}/history`),
  sessions: (query: {
    branchId?: number;
    area?: string;
    level?: number;
    fromUtc: string;
    toUtc: string;
    page: number;
  }) => request<PlayerSession[]>("GET", `${base}/players`, undefined, query),
  createSession: (r: {
    branchId: number;
    area: string;
    level: number;
    startsAtUtc: string;
    endsAtUtc: string;
    capacity: number;
    note: string;
  }) => request<number>("POST", `${base}/players`, r),
  sessionAction: (id: number, action: "join" | "leave" | "close") =>
    request("POST", `${base}/players/${id}/${action}`),
  sessionMembers: (id: number) =>
    request<SessionMember[]>("GET", `${base}/players/${id}/members`),
  decide: (id: number, userId: number, accept: boolean) =>
    request("PUT", `${base}/players/${id}/members/${userId}`, accept),
  expense: (id: number) =>
    request<Expense | null>("GET", `${base}/expenses/${id}`),
  expenseBookings: (groupId: number, page: number) =>
    request<
      {
        id: number;
        code: string;
        date: string;
        status: string;
        payableAmount: number;
      }[]
    >("GET", `${base}/expenses/bookings`, undefined, { groupId, page }),
  split: (id: number, ids: number[]) =>
    request("POST", `${base}/expenses/${id}`, ids),
  contribution: (id: number, userId: number, paid: boolean) =>
    request("PUT", `${base}/expenses/${id}/members/${userId}`, paid),
  equipment: (branchId: number) =>
    request<Equipment[]>("GET", `${base}/equipment`, undefined, { branchId }),
  saveEquipment: (id: number | null, r: Omit<Equipment, "id" | "available">) =>
    request<number>(
      id ? "PUT" : "POST",
      `${base}/equipment${id ? `/${id}` : ""}`,
      r,
    ),
  reserveEquipment: (r: {
    itemId: number;
    quantity: number;
    dueAtUtc: string;
    requestKey: string;
  }) => request<number>("POST", `${base}/equipment/orders`, r),
  equipmentOrders: (staff: boolean, branchId: number, page: number) =>
    request<EquipmentOrder[]>("GET", `${base}/equipment/orders`, undefined, {
      staff,
      branchId,
      page,
    }),
  equipmentAction: (id: number, action: string) =>
    request("POST", `${base}/equipment/orders/${id}/${action}`),
  coaches: (branchId: number) =>
    request<Coach[]>("GET", `${base}/coaches`, undefined, { branchId }),
  saveCoach: (id: number | null, r: Omit<Coach, "id">) =>
    request<number>(
      id ? "PUT" : "POST",
      `${base}/coaches${id ? `/${id}` : ""}`,
      r,
    ),
  coachSlots: (id: number, fromUtc: string, toUtc: string) =>
    request<CoachSlot[]>("GET", `${base}/coaches/${id}/slots`, undefined, {
      fromUtc,
      toUtc,
    }),
  addCoachSlot: (
    id: number,
    r: {
      startsAtUtc: string;
      endsAtUtc: string;
      capacity: number;
      pricePerPerson: number;
      courtBookingId?: number;
    },
  ) => request<number>("POST", `${base}/coaches/${id}/slots`, r),
  cancelCoachSlot: (id: number) =>
    request("POST", `${base}/coaches/slots/${id}/cancel`),
  reserveCoach: (r: { slotId: number; people: number; requestKey: string }) =>
    request<number>("POST", `${base}/coaches/reservations`, r),
  coaching: (staff: boolean, branchId: number, page: number) =>
    request<Coaching[]>("GET", `${base}/coaches/reservations`, undefined, {
      staff,
      branchId,
      page,
    }),
  coachingAction: (id: number, action: string) =>
    request("POST", `${base}/coaches/reservations/${id}/${action}`),
  tournaments: (branchId: number, page: number) =>
    request<Tournament[]>("GET", `${base}/tournaments`, undefined, {
      branchId,
      page,
    }),
  createTournament: (r: { branchId: number; name: string; maxTeams: number }) =>
    request<number>("POST", `${base}/tournaments`, r),
  tournament: (id: number) =>
    request<TournamentDetail>("GET", `${base}/tournaments/${id}`),
  registerTeam: (id: number, name: string, players: string) =>
    request("POST", `${base}/tournaments/${id}/teams`, { name, players }),
  withdrawTeam: (id: number) =>
    request("DELETE", `${base}/tournaments/${id}/teams/mine`),
  draw: (id: number, pools: number) =>
    request("POST", `${base}/tournaments/${id}/draw`, pools),
  schedule: (
    id: number,
    matchId: number,
    r: { startsAtUtc: string; endsAtUtc: string; location: string },
  ) =>
    request("PUT", `${base}/tournaments/${id}/matches/${matchId}/schedule`, r),
  score: (
    id: number,
    matchId: number,
    scoreA: number,
    scoreB: number,
    reason?: string,
    expectedRevision?: number,
  ) =>
    request("PUT", `${base}/tournaments/${id}/matches/${matchId}/score`, {
      scoreA,
      scoreB,
      reason,
      expectedRevision,
    }),
  cancelTournament: (id: number) =>
    request("POST", `${base}/tournaments/${id}/cancel`),
  subscription: () =>
    request<{
      active: boolean;
      lastFetchedAtUtc?: string | null;
      changedAtUtc?: string | null;
    }>("GET", `${base}/calendar`),
  rotateCalendar: () => request<{ token: string }>("POST", `${base}/calendar`),
  revokeCalendar: () => request("DELETE", `${base}/calendar`),
};
export function calendarFeedUrl(token: string) {
  return new URL(
    `${apiBase(import.meta.env.VITE_API_URL)}${base}/calendar/feed/${token}.ics`,
    window.location.origin,
  ).href;
}
