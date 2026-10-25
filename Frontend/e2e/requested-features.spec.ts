import { test, expect, type Page } from "@playwright/test";
import { mockApi, ok, paged } from "./mockApi";

async function identity(page: Page, role = "Customer") {
  await page.addInitScript((r) => {
    localStorage.setItem("bb_token", "test-token");
    sessionStorage.setItem("bb_user", JSON.stringify({ id: 1, fullName: "Test User", phone: "0900000000", role: r }));
  }, role);
}
const court = { id: 1, branchId: 1, name: "Sân 1", code: "S01", isActive: true, priceLowPeak: 100000, pricePeak: 150000, weekendSurcharge: 0, capacity: 10, surface: "Sindalo", ratingAvg: 5, ratingCount: 1 };

test("all reviews loads reviews from every court", async ({ page }) => {
  await identity(page);
  await mockApi(page, [
    { pattern: /\/courts$/, respond: () => ok([court, { ...court, id: 2, name: "Sân 2" }]) },
    { pattern: /\/reviews\/paged\?/, respond: (_r, u) => {
      const selected = u.searchParams.get("courtId");
      const ids = selected ? [Number(selected)] : [1, 2];
      return paged(ids.map(id => ({ id, courtId: id, courtName: `Sân ${id}`, userName: `Khách ${id}`, rating: 5, createdAt: "2026-10-20T10:00:00Z", comment: `Nhận xét ${id}` })));
    } },
  ]);
  await page.goto("/reviews");
  await expect(page.getByText("Nhận xét 1")).toBeVisible();
  await expect(page.getByText("Nhận xét 2")).toBeVisible();
  await page.getByRole("button", { name: "Sân 1", exact: true }).click();
  await expect(page.getByText("Nhận xét 2")).toHaveCount(0);
});

test("account saves self profile and shows membership", async ({ page }) => {
  await identity(page);
  let profile = { id: 1, fullName: "Test User", phoneNumber: "0900000000", email: "", avatarUrl: null, tier: "Gold", membershipExpiresAt: "2099-12-31T00:00:00Z", multiplier: 2 };
  await mockApi(page, [
    { pattern: /\/account\/profile$/, method: "GET", respond: () => ok(profile) },
    { pattern: /\/account\/profile$/, method: "PATCH", respond: (_r, _u, body) => { profile = { ...profile, ...body as object }; return ok(profile); } },
    { pattern: /\/account\/summary$/, respond: () => ok({ pointsBalance: 2000, pointsRedeemableVnd: 40000 }) },
  ]);
  await page.goto("/account");
  await expect(page.getByText("Membership · Gold · 2× điểm")).toBeVisible();
  await page.getByRole("button", { name: "Chỉnh sửa tài khoản" }).click();
  await page.getByLabel("Họ và tên").fill("Tên mới");
  await page.getByLabel("Email", { exact: true }).fill("new@example.test");
  await page.getByRole("button", { name: "Lưu tài khoản" }).click();
  await expect(page.getByText("Đã cập nhật tài khoản.")).toBeVisible();
  await page.screenshot({ path: "test-results/account-updated.png", fullPage: true });
  await page.reload();
  await expect(page.getByText("new@example.test")).toBeVisible();
});

test("token scanner input submits token and displays real booking id", async ({ page }) => {
  await identity(page, "Manager");
  let body: unknown;
  await mockApi(page, [
    { pattern: /\/checkin\/token$/, method: "POST", respond: (_r, _u, b) => { body = b; return ok({ bookingId: 42, message: "OK" }); } },
  ]);
  await page.goto("/checkin");
  await page.getByLabel("Token check-in (quét / dán)").fill("server-issued-token");
  await page.getByLabel("Token check-in (quét / dán)").press("Enter");
  await expect(page.getByText("Check-in đơn 42 thành công.")).toBeVisible();
  expect(body).toEqual({ token: "server-issued-token" });
});

test("already booked slots offer waitlist without booking submission", async ({ page }) => {
  await identity(page);
  let joined = false;
  await mockApi(page, [
    { pattern: /\/branches$/, respond: () => ok([{ id: 1, name: "Cơ sở 1", isActive: true }]) },
    { pattern: /\/bookings\/availability\//, respond: () => ok([{ courtId: 1, courtName: "Sân 1", priceLowPeak: 100000, pricePeak: 150000, slots: [{ slotId: 1, label: "18:00", startTime: "18:00", endTime: "19:00", isPeak: false, isLocked: true, lockReason: "Đã đặt" }] }]) },
    { pattern: /\/waitlist\/join$/, method: "POST", respond: () => { joined = true; return ok({ id: 1 }); } },
  ]);
  await page.goto("/book");
  await page.getByRole("button", { name: "18:00 Đã đặt", exact: true }).click();
  await expect(page.getByRole("button", { name: "Xác nhận đặt sân" })).toBeDisabled();
  await page.getByRole("button", { name: "Tham gia danh sách chờ" }).click();
  await expect(page.getByText("Đã xếp bạn vào danh sách chờ (FIFO).")).toBeVisible();
  expect(joined).toBe(true);
});

test("court closure sends selected hours and renders status colors", async ({ page }) => {
  await identity(page, "Admin");
  let savedUrl: URL | undefined;
  let status = "Open";
  await mockApi(page, [
    { pattern: /\/courts$/, respond: () => ok([court]) },
    { pattern: /\/maintenance\/.+\/schedule$/, respond: () => ok({ status, startTime: status === "Open" ? null : "14:00", endTime: "16:00" }) },
    { pattern: /\/maintenance\?/, method: "POST", respond: (_r, url) => { savedUrl = url; status = url.searchParams.get("status")!; return ok(null); } },
  ]);
  await page.goto("/admin/courts");
  await expect(page.getByText("Mở", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Bảo trì", exact: true }).click();
  await expect(page.getByRole("button", { name: "Mở (bình thường)" })).toHaveCSS("color", "rgb(23, 99, 69)");
  await page.locator('button[aria-pressed]').filter({ hasText: "Bảo trì" }).click();
  await expect(page.locator('button[aria-pressed]').filter({ hasText: "Bảo trì" })).toHaveCSS("color", "rgb(245, 158, 11)");
  await page.getByRole("button", { name: "Đóng", exact: true }).filter({ hasText: "Đóng" }).click();
  await expect(page.getByRole("button", { name: "Đóng", exact: true }).filter({ hasText: "Đóng" })).toHaveCSS("color", "rgb(239, 68, 68)");
  await page.getByLabel("Từ giờ").fill("14:00");
  await page.getByLabel("Đến giờ").fill("16:00");
  await page.screenshot({ path: "test-results/maintenance-hours.png", fullPage: true });
  await page.getByRole("button", { name: "Lưu trạng thái" }).click();
  await expect(page.getByText("Đã lưu trạng thái bảo trì.", { exact: false })).toBeVisible();
  expect(savedUrl?.searchParams.get("startTime")).toBe("14:00");
  expect(savedUrl?.searchParams.get("endTime")).toBe("16:00");
  await expect(page.getByText("Đóng", { exact: true })).toBeVisible();
});

test("membership selects customers by name and phone", async ({ page }) => {
  await identity(page, "Admin");
  let assigned: unknown;
  await mockApi(page, [
    { pattern: /\/membership\/customers$/, respond: () => ok([{ id: 3, fullName: "Nguyễn An", phoneNumber: "0900000003", tier: "None", multiplier: 1, lapsed: false }]) },
    { pattern: /\/membership\/assign$/, method: "POST", respond: (_r, _u, b) => { assigned = b; return ok(null); } },
  ]);
  await page.goto("/admin/membership");
  await page.getByRole("button", { name: "Gán hạng" }).click();
  await page.getByLabel("Khách hàng", { exact: true }).selectOption("3");
  await page.getByRole("button", { name: "Gán", exact: true }).click();
  await expect(page.getByLabel("Khách hàng", { exact: true })).toHaveCount(0);
  expect(assigned).toEqual({ userId: 3, tier: "Silver" });
});

test("expired vouchers display expiry and cannot be manually locked again", async ({ page }) => {
  await identity(page, "Admin");
  await mockApi(page, [
    { pattern: /\/vouchers\?/, respond: () => ok([{ id: 1, code: "OLD", type: "Fixed", value: 10000, minSpend: 0, usedQty: 0, totalQty: 10, validUntil: "2000-01-01", isActive: true }]) },
  ]);
  await page.goto("/admin/vouchers");
  await expect(page.getByText("Hết hạn sử dụng", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "Khóa", exact: true })).toHaveCount(0);
});

test("empty reviews direct customers to completed bookings", async ({ page }) => {
  await identity(page);
  await mockApi(page, [
    { pattern: /\/courts$/, respond: () => ok([court]) },
    { pattern: /\/reviews\/paged\?/, respond: () => paged([]) },
  ]);
  await page.goto("/reviews");
  await expect(page.getByRole("link", { name: "Đánh giá đơn đã hoàn thành" })).toHaveAttribute("href", "/bookings");
});
