import type { QueryClient, QueryKey } from "@tanstack/react-query";
import { api } from "../services/api";
import type { AuthUser } from "../store/auth";
import { pageLoaders } from "./pageLoaders";

export function warmSession(client: QueryClient, user: AuthUser) {
  let stopped = false;
  const load = async <T,>(key: QueryKey, fn: () => Promise<T>) => {
    if (stopped) return undefined;
    return client.fetchQuery({ queryKey: key, queryFn: fn });
  };
  const tasks: (() => Promise<unknown>)[] = [];
  const add = <T,>(key: QueryKey, fn: () => Promise<T>) => tasks.push(() => load(key, fn));
  const staff = user.role === "Admin" || user.role === "Manager";
  const modules = [pageLoaders.Home, pageLoaders.Book, pageLoaders.Bookings, pageLoaders.Detail,
    pageLoaders.Account, pageLoaders.Waitlist, pageLoaders.Groups, pageLoaders.Reviews];
  if (staff) modules.push(pageLoaders.Checkin, pageLoaders.Dashboard, pageLoaders.AdminBookings,
    pageLoaders.Courts, pageLoaders.Vouchers, pageLoaders.Slots);
  if (user.role === "Admin") modules.push(pageLoaders.Branches, pageLoaders.Staff, pageLoaders.Audit, pageLoaders.Membership);
  void Promise.allSettled(modules.map((fn) => fn()));

  const day = new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh", year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date());
  tasks.push(async () => {
    const branches = await load(["branches"], api.branches.list);
    // The booking page opens the first branch; other dates/branches load on demand.
    if (branches?.[0]) await load(["availability", branches[0].id, day], () => api.booking.availability(branches[0].id, day));
  });
  add(["reviews", "paged", null, 1], () => api.reviews.paged(null, 1));
  tasks.push(async () => {
    const mine = await load(["my-bookings", "", 1], () => api.booking.mine(undefined, 1));
    for (const b of mine?.items.slice(0, 3) ?? []) {
      await load(["booking", b.id], () => api.booking.getById(b.id));
      await load(["payments", b.id], () => api.payments.forBooking(b.id));
      if (b.status === "Completed") await load(["reviews", "booking", b.id], () => api.reviews.forBooking(b.id));
    }
  });
  add(["account", "summary"], api.account.summary);
  add(["account", "profile", user.id], api.account.profile);
  add(["payment-methods"], api.payments.methods);
  add(["groups", "mine"], api.groups.mine);
  add(["waitlist", "mine", 1], () => api.waitlist.paged(1));
  if (staff) {
    add(["admin-bookings", "", "", 1, "", "", false], () => api.booking.all(undefined, undefined, 1, 10));
    add(["report", "summary"], () => api.admin.report.summary());
    add(["time-slots", user.role === "Admin"], () => api.timeSlots.list(user.role === "Admin"));
    add(["vouchers"], () => api.vouchers.list(false));
  }
  if (user.role === "Admin") {
    add(["membership", "members"], api.admin.membership.members);
    add(["membership", "customers"], api.admin.membership.customers);
    add(["staff"], api.admin.staff.list);
    add(["audit", "", 1], () => api.admin.audit.query({ page: 1, pageSize: 100 }));
    const to = day;
    const from = new Date(Date.now() + 7 * 3600e3 - 6 * 864e5).toISOString().slice(0, 10);
    add(["report", "revenue", from, to], () => api.admin.report.revenue(from, to));
    add(["report", "occupancy", from, to], () => api.admin.report.occupancy(from, to));
  }
  const worker = async () => {
    while (!stopped && tasks.length) {
      try { await tasks.shift()!(); } catch { /* Each page retains its error/retry UI. */ }
    }
  };
  void Promise.allSettled([worker(), worker(), worker()]);
  return () => { stopped = true; };
}
