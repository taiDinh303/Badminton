import { test, expect } from "@playwright/test";
import { mockApi, ok, seedLoggedInCustomer } from "./mockApi";

test("partial refund shows the amount actually returned", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Customer", role: "Customer", phone: "0900000001" });
  await mockApi(page, [
    { pattern: /\/bookings\/1$/, respond: () => ok({ id: 1, userId: 1, courtId: 1, courtName: "Court", bookingCode: "REFUND", date: "2026-10-25", slotLabel: "12:00", status: "Cancelled", playerCount: 2 }) },
    { pattern: /\/payments\/booking\/1$/, respond: () => ok([{ id: 1, bookingId: 1, method: "Cash", amount: 100000, status: "PartialRefund", refundAmount: 50000, refundedAt: "2026-10-24T18:00:00Z" }]) },
  ]);
  await page.goto("/bookings/1");
  await expect(page.getByText(/hoàn 50\.000đ/)).toBeVisible();
});

test("maintenance defaults to the Vietnam day at midnight", async ({ page }) => {
  await page.clock.install({ time: new Date("2026-10-24T18:00:00Z") });
  await seedLoggedInCustomer(page, { id: 1, fullName: "Admin", role: "Admin", phone: "0900000001" });
  await mockApi(page, [
    { pattern: /\/maintenance\/.+\/schedule$/, respond: () => ok({ status: "Open", startTime: null, endTime: null }) },
    { pattern: /\/branches$/, respond: () => ok([{ id: 1, name: "Branch", isActive: true }]) },
    { pattern: /\/courts(?:\?|$)/, respond: () => ok([{ id: 1, branchId: 1, code: "C1", name: "Court", isActive: true, priceLowPeak: 100000, pricePeak: 150000, weekendSurcharge: 0.2, capacity: 2, surface: "Sindalo", ratingCount: 0 }]) },
  ]);
  await page.goto("/admin/courts");
  await page.getByRole("button", { name: /Bảo trì/ }).first().click();
  await expect(page.getByRole("dialog").locator('input[type="date"]')).toHaveValue("2026-10-25");
});

test("reconciliation queries the saved refund instead of accepting an amount from the browser", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Manager", role: "Manager", phone: "0900000001" });
  let calls = 0;
  await mockApi(page, [
    { pattern: /\/payments\/reconciliation\?/, respond: () => ok({ items: calls ? [] : [{ payment: { id: 9, bookingId: 1, method: "MoMo", amount: 100000, status: "Success", gatewayRef: "PAY1" }, reason: "refund_uncertain", createdAt: "2026-10-24T00:00:00Z" }], totalCount: calls ? 0 : 1 }) },
    { pattern: /\/payments\/reconciliation\/9\/refund$/, method: "POST", respond: (_route, _url, body) => {
      expect(body).toBeNull(); calls++; return ok({ id: 10, status: "Refunded", refundAmount: 100000 });
    } },
  ]);
  await page.goto("/admin/reconciliation");
  await page.getByRole("button", { name: "Truy vấn kết quả hoàn tiền", exact: true }).click();
  await expect(page.getByText("Đã xác nhận hoàn tiền với provider.")).toBeVisible();
  await expect(page.getByText("Không có giao dịch cần đối soát.")).toBeVisible();
  expect(calls).toBe(1);
});
