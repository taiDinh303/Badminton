import { test, expect } from "./fixtures";
import { mockApi, ok, err, seedLoggedInCustomer } from "./mockApi";

const branch = { id: 1, name: "Cầu lông Hà Đông", address: "Hà Đông, Hà Nội", isActive: true, openTime: "06:00", closeTime: "23:00", courtCount: 3 };
const courts = [1, 2, 3].map(id => ({ id, branchId: 1, name: `Sân ${id}`, isActive: true, capacity: 10, priceLowPeak: 100000, pricePeak: 150000 }));
const availability = courts.map(c => ({ courtId: c.id, courtName: c.name, capacity: 10, priceLowPeak: 100000, pricePeak: 150000,
  slots: Array.from({ length: 16 }, (_, i) => ({ slotId: i + 1, label: `${i + 6}:00–${i + 7}:00`, startTime: `${String(i + 6).padStart(2, "0")}:00`, endTime: `${String(i + 7).padStart(2, "0")}:00`, isPeak: i > 10, isLocked: i < 3 || i === 6 || i === 8, lockReason: i < 3 ? "Đã qua giờ đặt" : i === 6 ? "Đã đặt" : i === 8 ? "Bảo trì" : "" })) }));
const price = { basePrice: 100000, weekendSurcharge: 0, voucherDiscount: 0, pointsDiscount: 0, total: 100000, payable: 100000 };

test("booking errors can be retried and changing date closes the stale selection", async ({ page }) => {
  await page.clock.setFixedTime(new Date("2026-10-26T01:00:00Z"));
  await seedLoggedInCustomer(page, { id: 1, fullName: "Khách kiểm thử", role: "Customer", phone: "0900000001" });
  let calendarAvailable = false;
  let quoteReads = 0;
  await mockApi(page, [
    { pattern: /\/branches$/, respond: () => ok([branch]) },
    { pattern: /\/bookings\/availability\//, respond: () => !calendarAvailable ? err(503, "UNAVAILABLE", "Lịch tạm ngưng") : ok(availability) },
    { pattern: /\/bookings\/quote$/, respond: () => ++quoteReads === 1 ? err(405, "METHOD", "HTTP 405") : ok(price) },
    { pattern: /./, respond: () => ok([]) },
  ]);
  await page.goto("/book");
  await expect(page.getByText("Không tải được lịch sân.")).toBeVisible();
  await expect(page.getByText("Chưa có sân trống vào ngày này.")).toHaveCount(0);
  calendarAvailable = true;
  await page.getByRole("button", { name: "Thử lại", exact: true }).click();
  await page.getByRole("button", { name: "09:00 Trống", exact: true }).first().click();
  const dialog = page.getByRole("dialog", { name: "Xác nhận đặt sân" });
  await expect(dialog.getByText("HTTP 405")).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Xác nhận đặt sân" })).toBeDisabled();
  await dialog.getByRole("button", { name: "Thử lại giá" }).click();
  await expect(dialog.getByRole("button", { name: "Xác nhận đặt sân" })).toBeEnabled();
  await page.getByRole("button", { name: "Ngày sau" }).click();
  await expect(dialog).toHaveCount(0);
});

for (const width of [320, 390, 1440, 1920]) {
  test(`populated schedule and auth visual check ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 960 });
    await seedLoggedInCustomer(page, { id: 1, fullName: "Khách kiểm thử", role: "Customer", phone: "0900000001" });
    await mockApi(page, [
      { pattern: /\/branches$/, respond: () => ok([branch]) },
      { pattern: /\/bookings\/availability\//, respond: () => ok(availability) },
      { pattern: /./, respond: () => ok([]) },
    ]);
    await page.goto("/book");
    await expect(page.getByText("Sân 3", { exact: true })).toBeVisible();
    await expect(page.locator(".slot-cell")).toHaveCount(48);
    await expect(page.locator(".header-title > p")).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
    await page.screenshot({ path: `test-results/schedule-${width}.png`, fullPage: true });
    await page.goto("/auth");
    await expect(page.getByLabel("Số điện thoại")).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
    await page.screenshot({ path: `test-results/auth-polish-${width}.png`, fullPage: true });
  });
}
