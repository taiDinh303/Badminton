import { test, expect } from "@playwright/test";
import { mockApi, ok, paged, seedLoggedInCustomer } from "./mockApi";

test("an admin can review their own completed booking with a comment, once", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Admin", role: "Admin", phone: "0900000001" });
  let reviews: object[] = [];
  let posted: unknown;
  await mockApi(page, [
    { pattern: /\/bookings\/301$/, respond: () => ok({ id: 301, userId: 1, courtId: 1, bookingCode: "BK-301", status: "Completed", courtName: "Sân 1", date: "2026-10-21", slotLabel: "18:00", playerCount: 2 }) },
    { pattern: /\/payments\/booking\/301$/, respond: () => ok([]) },
    { pattern: /\/reviews\/booking\/301$/, respond: () => ok(reviews[0] ?? null) },
    { pattern: /\/reviews$/, method: "POST", respond: (_r, _u, body) => { posted = body; const r = { id: 1, ...(body as object), courtId: 1 }; reviews = [r]; return ok(r); } },
  ]);
  await page.goto("/bookings/301");
  await page.getByRole("tab", { name: "5", exact: true }).click();
  await page.getByLabel("Nhận xét", { exact: true }).fill("Sân sạch, ánh sáng tốt.");
  await page.getByRole("button", { name: "Gửi đánh giá", exact: true }).click();
  await expect(page.getByText("Đơn này đã được đánh giá.")).toBeVisible();
  expect(posted).toEqual({ bookingId: 301, rating: 5, comment: "Sân sạch, ánh sáng tốt." });
  await page.reload();
  await expect(page.getByText("Sân sạch, ánh sáng tốt.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Gửi đánh giá", exact: true })).toHaveCount(0);
});

test("warm customer navigation reuses data, stays within permissions and leaves other voucher codes enabled", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 7, fullName: "Customer", role: "Customer", phone: "0900000007" });
  const requests: string[] = [];
  page.on("request", r => { if (r.url().includes("/api/v1/")) requests.push(new URL(r.url()).pathname); });
  await mockApi(page, [
    { pattern: /\/branches$/, respond: () => ok([{ id: 1, name: "Cơ sở A", isActive: true, openTime: "06:00", closeTime: "23:00", courtCount: 1 }]) },
    { pattern: /\/courts$/, respond: () => ok([{ id: 1, name: "Sân A", branchId: 1, isActive: true }]) },
    { pattern: /\/bookings\/availability\//, respond: () => ok([{ courtId: 1, courtName: "Sân A", priceLowPeak: 100000, pricePeak: 150000, slots: [{ slotId: 1, label: "18:00", startTime: "18:00", endTime: "19:00", isPeak: true, isLocked: false }] }]) },
    { pattern: /\/account\/summary$/, respond: () => ok({ pointsBalance: 0, voucherUsed: true, usedVoucher: { code: "OLD" }, availableVouchers: [], nextVoucher: null }) },
    { pattern: /\/reviews\/paged\?/, respond: () => paged([{ id: 1, bookingId: 1, rating: 5, userName: "Khách", courtName: "Sân A", comment: "Đánh giá đã tải trước", createdAt: "2026-10-20T00:00:00Z" }]) },
    { pattern: /\/groups\/mine$/, respond: () => ok([]) },
  ]);
  await page.goto("/dashboard");
  await expect.poll(() => requests.filter(p => p.endsWith('/reviews/paged')).length).toBe(1);
  await page.getByRole("link", { name: "Đánh giá", exact: true }).click();
  await expect(page.getByText("Đánh giá đã tải trước")).toBeVisible();
  expect(requests.filter(p => p.endsWith('/reviews/paged')).length).toBe(1);
  expect(requests.some(p => p.includes('/admin/'))).toBe(false);
  await page.getByRole("link", { name: "Đặt sân", exact: true }).click();
  await expect(page.getByText(/T[2-7] · \d{2}\/\d{2}\/\d{4}/)).toHaveCount(0);
  await page.getByRole("button", { name: /18:00 Trống/ }).click();
  await expect(page.getByLabel("Mã voucher")).toBeEnabled();
  await expect(page.getByText(/Mỗi mã voucher chỉ dùng một lần/)).toBeVisible();
});
