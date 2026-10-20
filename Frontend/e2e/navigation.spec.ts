import { test, expect } from "@playwright/test";
import { mockApi, ok, err, seedLoggedInCustomer } from "./mockApi";

test("mobile manager can reach check-in from the operations menu", async ({ page }) => {
  await page.setViewportSize({ width: 375, height: 812 });
  await seedLoggedInCustomer(page, { id: 8, fullName: "Quản lý cơ sở", role: "Manager", phone: "0900000008" });
  await mockApi(page, [{ pattern: /\/branches$/, method: "GET", respond: () => ok([]) }]);
  await page.goto("/");
  await page.getByRole("button", { name: "Mở menu" }).click();
  const menu = page.getByRole("navigation", { name: "Menu di động" });
  await expect(menu.getByRole("link", { name: "Check-in", exact: true })).toBeVisible();
  await expect(menu.getByRole("link", { name: "Nhân viên", exact: true })).toHaveCount(0);
  await page.screenshot({ path: "test-results/mobile-manager-menu.png", fullPage: true });
  await menu.getByRole("link", { name: "Check-in", exact: true }).click();
  await expect(page).toHaveURL(/\/checkin$/);
  await expect(menu).toHaveCount(0);
});

test("manager cannot open admin staff route directly", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 8, fullName: "Quản lý", role: "Manager", phone: "0900000008" });
  await mockApi(page, [{ pattern: /\/branches$/, method: "GET", respond: () => ok([]) }]);
  await page.goto("/admin/staff");
  await expect(page).toHaveURL(/\/$/);
});

test("an expired session clears identity and returns to login", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 7, fullName: "Khách hàng", role: "Customer", phone: "0900000007" });
  await mockApi(page, [{ pattern: /\/branches$/, method: "GET", respond: () => err(401, "UNAUTHORIZED", "Phiên hết hạn") }]);
  await page.goto("/");
  await expect(page).toHaveURL(/\/auth$/);
  expect(await page.evaluate(() => localStorage.getItem("bb_token"))).toBeNull();
  expect(await page.evaluate(() => sessionStorage.getItem("bb_user"))).toBeNull();
});
