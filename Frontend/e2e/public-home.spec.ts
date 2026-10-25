import { test, expect } from "@playwright/test";
import { mockApi, ok, seedLoggedInCustomer } from "./mockApi";

for (const width of [320, 390, 768, 1440]) {
  test(`public home works without a session at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 900 });
    const apiRequests: string[] = [];
    page.on("request", req => { if (req.url().includes("/api/v1/")) apiRequests.push(req.url()); });
    await page.goto("/");
    await expect(page).toHaveURL(/\/home$/);
    await expect(page.getByRole("heading", { name: /Bớt chờ đợi/ })).toBeVisible();
    await expect(page.locator("aside")).toHaveCount(0);
    await expect(page.getByRole("link", { name: "Đăng nhập", exact: true })).toBeVisible();
    await expect(page.getByRole("link", { name: "Đăng ký", exact: true })).toBeVisible();
    await expect(page.getByRole("navigation", { name: "Điều hướng chính", exact: true })).toHaveCount(0);
    expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
    await page.screenshot({ path: `test-results/public-home-${width}.png`, fullPage: true });
    await page.reload();
    await expect(page.getByRole("heading", { name: /Bớt chờ đợi/ })).toBeVisible();
    expect(apiRequests).toHaveLength(0);
    await page.getByRole("link", { name: "Đăng ký", exact: true }).click();
    await expect(page).toHaveURL(/\/auth\?mode=register$/);
    await expect(page.getByRole("heading", { name: "Tạo tài khoản khách" })).toBeVisible();
    await page.getByRole("link", { name: /Về trang chủ/ }).click();
    await expect(page).toHaveURL(/\/home$/);
  });
}

test("booking intent survives login", async ({ page }) => {
  await mockApi(page, [
    { pattern: /\/auth\/login$/, method: "POST", respond: () => ok({ userId: 7, fullName: "Khách đặt", phoneNumber: "0900000007", role: "Customer", accessToken: "test-token", expiresInMinutes: 60 }) },
    { pattern: /\/branches$/, respond: () => ok([]) },
  ]);
  await page.goto("/home");
  await page.getByRole("link", { name: "Đặt sân ngay" }).click();
  await expect(page).toHaveURL(/\/auth$/);
  await page.getByLabel("Số điện thoại", { exact: true }).fill("0900000007");
  await page.getByLabel("Mật khẩu", { exact: true }).fill("Pass@123");
  await page.locator("form").getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page).toHaveURL(/\/book$/);
});

test("branch shortcut selects that branch on mobile", async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await seedLoggedInCustomer(page, { id: 7, fullName: "Khách đặt", role: "Customer", phone: "0900000007" });
  await mockApi(page, [{ pattern: /\/branches$/, respond: () => ok([
    { id: 1, name: "Cơ sở A", isActive: true, openTime: "06:00", closeTime: "23:00" },
    { id: 2, name: "Cơ sở B", isActive: true, openTime: "06:00", closeTime: "23:00" },
  ]) }]);
  await page.goto("/dashboard");
  await expect(page.getByRole("heading", { name: "Khách đặt", exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth - innerWidth)).toBeLessThanOrEqual(1);
  await page.getByRole("button", { name: "Menu tài khoản" }).click();
  await expect(page.getByRole("navigation", { name: "Tài khoản", exact: true })).toBeVisible();
  await page.screenshot({ path: "test-results/customer-mobile-avatar.png", fullPage: true });
  await page.keyboard.press("Escape");
  await page.locator('a[href="/book?branchId=2"]').click();
  await expect(page).toHaveURL(/\/book\?branchId=2$/);
  await expect(page.getByRole("combobox").first()).toHaveValue("2");
});

test("customer clock, avatar keyboard/outside dismissal and logout", async ({ page }) => {
  await page.clock.setFixedTime(new Date("2026-10-23T05:00:00Z"));
  await seedLoggedInCustomer(page, { id: 7, fullName: "Khách đặt", role: "Customer", phone: "0900000007" });
  await mockApi(page, [{ pattern: /\/branches$/, respond: () => ok([]) }]);
  await page.goto("/dashboard");
  const clock = page.getByLabel("Giờ Việt Nam");
  await expect(clock).toHaveText("12:00:00");
  await page.clock.setFixedTime(new Date("2026-10-23T05:00:02Z"));
  await expect(clock).toHaveText("12:00:02");
  const avatar = page.getByRole("button", { name: "Menu tài khoản" });
  expect(await avatar.evaluate(el => getComputedStyle(el).cursor)).toBe("pointer");
  await avatar.focus();
  await page.keyboard.press("Enter");
  await expect(avatar).toHaveAttribute("aria-expanded", "true");
  await page.keyboard.press("Escape");
  await expect(avatar).toBeFocused();
  await expect(avatar).toHaveAttribute("aria-expanded", "false");
  await avatar.click();
  await page.getByRole("heading", { name: "Khách đặt", exact: true }).click();
  await expect(avatar).toHaveAttribute("aria-expanded", "false");
  await page.screenshot({ path: "test-results/customer-dashboard.png", fullPage: true });
  await avatar.click();
  await page.getByRole("navigation", { name: "Tài khoản", exact: true }).getByRole("button", { name: "Đăng xuất" }).click();
  await expect(page).toHaveURL(/\/home$/);
  expect(await page.evaluate(() => localStorage.getItem("bb_token"))).toBeNull();
  await expect(page.getByRole("link", { name: "Đăng nhập", exact: true })).toBeVisible();
  await page.getByRole("link", { name: "Đặt sân ngay" }).click();
  await expect(page).toHaveURL(/\/auth$/);
});
