import { test, expect } from "@playwright/test";

test.afterAll(async ({ request }) => { await request.post("/__test/shutdown"); });

test("real login, slot quote with voucher and points, booking, and owner cancellation", async ({ page }) => {
  // Every request reaches the isolated .NET host and database; no API interception.
  await page.goto("/auth");
  await page.getByLabel("Số điện thoại", { exact: true }).fill("0900000001");
  await page.getByLabel("Mật khẩu", { exact: true }).fill("BrowserOnly$2026");
  await page.locator("form").getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Alice", exact: true })).toBeVisible();
  await page.getByRole("link", { name: "Đặt sân", exact: true }).filter({ visible: true }).first().click();
  const day = new Date(); day.setUTCDate(day.getUTCDate() + 5);
  const date = day.toISOString().slice(0, 10);
  await page.getByLabel("Chọn ngày").fill(date);
  await page.getByRole("button", { name: /12:00/ }).first().click();
  const dialog = page.getByRole("dialog", { name: "Xác nhận đặt sân" });
  await dialog.getByLabel("Số người", { exact: true }).fill("1");
  const invalidQuote = page.waitForResponse(r => r.url().endsWith("/bookings/quote") && r.request().postDataJSON().voucherCode === "UNKNOWN");
  await dialog.getByLabel("Mã voucher").fill("UNKNOWN");
  expect((await invalidQuote).status()).toBe(400);
  await expect(dialog.getByText("Voucher không áp dụng cho đơn này.")).toBeVisible();
  await expect(dialog.getByRole("button", { name: "Xác nhận đặt sân", exact: true })).toBeDisabled();
  await dialog.getByLabel("Mã voucher").fill("browser");
  const quoted = page.waitForResponse(r => r.url().endsWith("/bookings/quote") && r.request().postDataJSON().pointsToRedeem === 1000);
  await dialog.getByLabel("Số điểm đổi").fill("1000");
  const response = await quoted;
  expect(response.ok()).toBe(true);
  const quote = (await response.json()).data;
  expect(quote.voucherDiscount).toBe(10000);
  expect(quote.pointsDiscount).toBe(20000);
  await expect(dialog.getByText(/Voucher: −10\.000/)).toBeVisible();
  const created = page.waitForResponse(r => r.url().endsWith("/api/v1/bookings") && r.request().method() === "POST");
  await dialog.getByRole("button", { name: "Xác nhận đặt sân", exact: true }).click();
  const bookingResponse = await created;
  expect(bookingResponse.ok()).toBe(true);
  const booking = (await bookingResponse.json()).data;
  expect(booking.totalAmount).toBe(quote.total);
  expect(booking.payableAmount).toBe(quote.payable);
  await expect(page.getByText(booking.bookingCode)).toBeVisible();
  await page.goto(`/bookings/${booking.id}`);
  const cancelled = page.waitForResponse(r => r.url().endsWith(`/bookings/${booking.id}/cancel`));
  await page.getByRole("button", { name: "Huỷ đặt chỗ", exact: true }).click();
  expect((await (await cancelled).json()).data.status).toBe("Cancelled");
  await expect(page.getByText("Đã hủy", { exact: true })).toBeVisible();
  await page.goto("/account");
  await page.getByLabel("Mật khẩu hiện tại", { exact: true }).fill("BrowserOnly$2026");
  await page.getByLabel("Mật khẩu mới", { exact: true }).fill("BrowserChanged$2026");
  await page.getByLabel("Nhập lại mật khẩu mới", { exact: true }).fill("BrowserChanged$2026");
  const changed = page.waitForResponse(r => r.url().endsWith("/auth/change-password"));
  await page.getByRole("button", { name: "Đổi mật khẩu và đăng xuất", exact: true }).click();
  expect((await changed).status()).toBe(200);
  await expect(page).toHaveURL(/\/auth$/);
  await expect(page.getByRole("status")).toContainText("Đã đổi mật khẩu");
  await page.getByLabel("Số điện thoại", { exact: true }).fill("0900000001");
  await page.getByLabel("Mật khẩu", { exact: true }).fill("BrowserChanged$2026");
  await page.locator("form").getByRole("button", { name: "Đăng nhập", exact: true }).click();
  // Gate restores the account page that required reauthentication.
  await expect(page.getByText("Alice", { exact: true }).filter({ visible: true }).first()).toBeVisible();
  const previousToken = await page.evaluate(() => localStorage.getItem("bb_token"));
  await page.goto("/account");
  const revoked = page.waitForResponse(r => r.url().endsWith("/auth/logout"));
  await page.getByRole("button", { name: "Đăng xuất mọi thiết bị", exact: true }).click();
  expect((await revoked).status()).toBe(200);
  await expect(page).toHaveURL(/\/auth$/);
  expect((await page.request.get("/api/v1/account/profile", { headers: { Authorization: `Bearer ${previousToken}` } })).status()).toBe(401);

});
