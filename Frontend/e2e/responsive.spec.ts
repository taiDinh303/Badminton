import { test, expect, type Page } from "@playwright/test";
import { mkdirSync, writeFileSync } from "node:fs";
import { mockApi, ok, seedLoggedInCustomer } from "./mockApi";

const branch = { id: 1, name: "Cơ sở cầu lông Nguyễn Văn Linh", address: "123 Nguyễn Văn Linh", isActive: true, openTime: "06:00", closeTime: "23:00", courtCount: 2 };
const court = { id: 1, branchId: 1, name: "Sân cầu lông số 1", code: "S01", isActive: true, priceLowPeak: 100000, pricePeak: 150000, weekendSurcharge: 0, capacity: 10, surface: "Sindalo", ratingAvg: 5, ratingCount: 1 };
const booking = { id: 1, userId: 1, bookingCode: "BK000001", courtId: 1, courtName: court.name, branchName: branch.name, date: "2026-10-24", slotLabel: "18:00–19:00", playerCount: 2, status: "Confirmed", payableAmount: 200000, basePrice: 200000, totalAmount: 200000, discountAmount: 0, pointsRedeemed: 0, pointsEarned: 2000, createdAt: "2026-10-22T08:00:00Z" };
const paged = { items: [booking], currentPage: 1, totalPages: 2, totalItems: 2, hasNextPage: true, hasPreviousPage: false };
const routes = ["/", "/dashboard", "/book", "/bookings", "/bookings/1", "/account", "/groups", "/reviews", "/waitlist", "/checkin", "/admin", "/admin/bookings", "/admin/courts", "/admin/vouchers", "/admin/slots", "/admin/branches", "/admin/staff", "/admin/audit", "/admin/membership"];

async function setup(page: Page, populated = false) {
  await page.clock.setFixedTime(new Date("2026-10-22T08:00:00Z"));
  // Keep screenshots independent of external font availability.
  await page.route(/fonts\.(googleapis|gstatic)\.com/, route => route.abort());
  await seedLoggedInCustomer(page, { id: 1, fullName: "Nguyễn Văn Khách Hàng", role: "Admin", phone: "0900000001" });
  await mockApi(page, [
    { pattern: /\/branches$/, respond: () => ok([branch]) },
    { pattern: /\/courts(?:\?|$)/, respond: () => ok([court]) },
    { pattern: /\/bookings\/availability\//, respond: () => ok([{ courtId: 1, courtName: court.name, priceLowPeak: 100000, pricePeak: 150000, slots: [{ slotId: 1, label: "18:00–19:00", startTime: "18:00", endTime: "19:00", isPeak: false, isLocked: false, lockReason: "" }] }]) },
    { pattern: /\/bookings\/(mine|all)/, respond: () => ok(paged) },
    { pattern: /\/bookings\/1$/, respond: () => ok(booking) },
    { pattern: /\/account\/profile$/, respond: () => ok({ id: 1, fullName: "Nguyễn Văn Khách Hàng", phoneNumber: "0900000001", email: "khachhang@example.test", tier: "Gold", multiplier: 2 }) },
    { pattern: /\/account\/summary$/, respond: () => ok({ pointsBalance: 250000, pointsRedeemableVnd: 5000000, vouchers: [] }) },
    { pattern: /\/admin\/report\/summary/, respond: () => ok({ revenueToday: 12500000, todayBookings: 25, usersToday: 20, activeCourts: 6, activeBranches: 3, peakFillRatePct: 75, pendingCount: 4, noShowCount30d: 2 }) },
    { pattern: /\/membership\/members$/, respond: () => ok([{ id: 2, fullName: "Nguyễn Văn Thành Viên", phoneNumber: "0900000002", tier: "Gold", multiplier: 2, expiresAt: "2026-12-01", lapsed: false }]) },
    { pattern: /\/membership\/customers$/, respond: () => ok([{ id: 2, fullName: "Nguyễn Văn Thành Viên", phoneNumber: "0900000002", tier: "Gold", multiplier: 2 }]) },
    { pattern: /\/admin\/audit/, respond: () => ok({ ...paged, items: [{ id: 1, entityType: "CourtDateStatus", entityId: "1@2026-10-24", action: "Update", actorName: "Quản trị viên", createdAt: "2026-10-22T08:00:00Z" }] }) },
    { pattern: /\/time-slots(?:\?|$)/, respond: () => ok([{ id: 1, label: "18:00–19:00", startTime: "18:00", endTime: "19:00", isPeak: true, isActive: true }]) },
    { pattern: /\/vouchers\?/, respond: () => ok([{ id: 1, code: "WELCOME", type: "Fixed", value: 10000, minSpend: 0, usedQty: 0, totalQty: 10, validUntil: "2027-01-01", isActive: true }]) },
    { pattern: /\/admin\/staff$/, respond: () => ok([{ id: 2, fullName: "Nhân viên cơ sở", phoneNumber: "0900000002", role: "Manager", isActive: true, branchId: 1, branchName: branch.name }]) },
    { pattern: /\/payments\/methods$/, respond: () => ok(["Cash", "QR"]) },
    { pattern: /\/groups\/mine$/, respond: () => ok(populated ? [{ id: 1, ownerId: 1, name: "Nhóm cầu lông cuối tuần Nguyễn Văn Linh", memberCount: 4, maxMembers: 10 }] : []) },
    { pattern: /\/reviews\?/, respond: () => ok(populated ? [{ id: 1, courtId: 1, courtName: court.name, userName: "Nguyễn Văn Khách Hàng", rating: 5, createdAt: "2026-10-22T08:00:00Z", comment: "Sân sạch sẽ, rộng rãi và thuận tiện cho nhóm bạn." }] : []) },
    { pattern: /\/waitlist\/mine$/, respond: () => ok(populated ? [{ id: 1, courtName: court.name, slotLabel: "18:00–19:00", date: "2026-10-24", playerCount: 4, status: "Promoted", promotedBookingId: 1 }] : []) },
    { pattern: /./, respond: () => ok([]) },
  ]);
}

async function ready(page: Page, path: string) {
  await page.goto(path);
  await expect(page.locator("h1")).toBeVisible();
  await expect(page.getByText(/Đang tải/)).toHaveCount(0);
  await page.evaluate(() => document.fonts.ready);
}

async function fits(page: Page) {
  expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
}

for (const width of [320, 360, 390, 430, 767]) {
  test(`mobile pages fit ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 844 });
    await setup(page, true);
    for (const path of routes) {
      await ready(page, path);
      await fits(page);
      await expect(page.getByRole("button", { name: "Mở menu" })).toBeVisible();
      if (["/dashboard", "/book", "/admin"].includes(path)) await page.screenshot({ path: `test-results/mobile-${width}-${path.replaceAll("/", "_") || "home"}.png`, fullPage: true });
    }
    await page.screenshot({ path: `test-results/mobile-${width}.png`, fullPage: true });
  });
}

test("mobile booking drawer and modal stay reachable on short landscape screens", async ({ page }) => {
  await page.setViewportSize({ width: 667, height: 375 });
  await setup(page);
  await ready(page, "/book");
  await page.getByRole("button", { name: "18:00 Trống", exact: true }).click();
  const drawer = page.getByRole("dialog", { name: "Xác nhận đặt sân" });
  await expect(drawer).toBeVisible();
  expect((await drawer.boundingBox())!.y).toBeGreaterThanOrEqual(0);
  await drawer.getByRole("button", { name: "Xác nhận đặt sân" }).scrollIntoViewIfNeeded();
  await expect(drawer.getByRole("button", { name: "Xác nhận đặt sân" })).toBeInViewport();
  await drawer.getByRole("button", { name: "Đóng" }).click();
  await ready(page, "/admin/vouchers");
  await page.getByRole("button", { name: /Tạo voucher/ }).click();
  const modal = page.getByRole("dialog");
  expect((await modal.boundingBox())!.y).toBeGreaterThanOrEqual(0);
  await expect(modal.getByRole("button", { name: "Đóng", exact: true })).toBeInViewport();
  await fits(page);
});

for (const width of [768, 1024, 1440]) {
  test(`desktop appearance unchanged ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 1000 });
    await setup(page);
    const dir = "../artifacts/responsive-desktop-baseline";
    mkdirSync(dir, { recursive: true });
    for (const path of routes) {
      await ready(page, path);
      const file = `${dir}/${width}-${path.replaceAll("/", "-") || "home"}.png`;
      const shot = await page.screenshot({ fullPage: true, animations: "disabled" });
      if (process.env.CAPTURE_DESKTOP_BASELINE) writeFileSync(file, shot);
      else if (process.env.VERIFY_DESKTOP_BASELINE) {
        writeFileSync(file.replace(".png", "-actual.png"), shot);
        // Ignore only tiny rasterization noise (observed: 1 RGB level at rounded
        // edges). No pixels beyond this tight color threshold may differ.
        expect(shot).toMatchSnapshot(file.split("/").at(-1)!, { threshold: 0.01, maxDiffPixels: 0 });
      }
      await expect(page.getByRole("button", { name: "Mở menu" })).toBeHidden();
      await expect(page.locator("aside")).toBeVisible();
    }
  });
}

test("phone login fits small portrait and landscape screens", async ({ page }) => {
  await page.route(/fonts\.(googleapis|gstatic)\.com/, route => route.abort());
  for (const viewport of [{ width: 320, height: 568 }, { width: 430, height: 932 }, { width: 667, height: 375 }]) {
    await page.setViewportSize(viewport);
    await page.goto("/auth");
    await expect(page.getByLabel("Số điện thoại")).toBeVisible();
    await fits(page);
  }
});

test.describe("touch phone", () => {
  test.use({ isMobile: true, hasTouch: true, viewport: { width: 390, height: 844 } });
  test("opens booking, scrolls the drawer and restores desktop layout after resize", async ({ page }) => {
    await setup(page);
    await ready(page, "/book");
    await page.getByRole("button", { name: "18:00 Trống", exact: true }).tap();
    const dialog = page.getByRole("dialog", { name: "Xác nhận đặt sân" });
    await dialog.getByLabel("Mã voucher").fill("MOBILE");
    await dialog.getByRole("button", { name: "Xác nhận đặt sân" }).scrollIntoViewIfNeeded();
    await expect(dialog.getByRole("button", { name: "Xác nhận đặt sân" })).toBeInViewport();
    await page.screenshot({ path: "test-results/mobile-booking-drawer.png" });
    await dialog.getByRole("button", { name: "Đóng" }).tap();
    await page.getByRole("button", { name: "Mở menu" }).tap();
    await expect(page.getByRole("navigation", { name: "Menu di động" })).toBeVisible();
    await page.setViewportSize({ width: 1440, height: 1000 });
    await expect(page.getByRole("navigation", { name: "Menu di động" })).toBeHidden();
    await expect(page.locator("aside")).toBeVisible();
  });

  test("table actions and membership form remain usable at 320px", async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 568 });
    await setup(page, true);
    await ready(page, "/admin/membership");
    const region = page.getByRole("region", { name: "Bảng dữ liệu, cuộn ngang để xem các cột" });
    expect(await region.evaluate(el => el.scrollWidth > el.clientWidth)).toBe(true);
    await region.evaluate(el => { el.scrollLeft = el.scrollWidth; });
    await page.getByRole("button", { name: "Đổi / gia hạn" }).tap();
    await expect(page.getByLabel("Khách hàng", { exact: true })).toHaveValue("2");
    await fits(page);
    await page.screenshot({ path: "test-results/mobile-membership-form.png", fullPage: true });
  });

  test("wide landscape phone keeps the mobile navigation", async ({ page }) => {
    await page.setViewportSize({ width: 932, height: 430 });
    await setup(page, true);
    await ready(page, "/book");
    await expect(page.locator("aside")).toBeHidden();
    await page.getByRole("button", { name: "Mở menu" }).tap();
    await expect(page.getByRole("navigation", { name: "Menu di động" })).toBeVisible();
    await fits(page);
  });
});
