import { test, expect } from "./fixtures";
import { mockApi, ok, paged, seedLoggedInCustomer } from "./mockApi";

test("check-in defaults to the Vietnam date around midnight", async ({ page }) => {
  await page.clock.setFixedTime(new Date("2026-10-24T18:00:00Z"));
  await seedLoggedInCustomer(page, { id: 1, fullName: "Manager", role: "Manager", phone: "0900000001" });
  await mockApi(page, [{ pattern: /./, respond: () => ok([]) }]);
  await page.goto("/checkin");
  await expect(page.getByLabel("Chọn ngày", { exact: true })).toHaveValue("2026-10-25");
});

test("admin search and playing date go to the server before pagination", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Admin", role: "Admin", phone: "0900000001" });
  const requests: URL[] = [];
  await mockApi(page, [
    { pattern: /\/bookings\/all\?/, respond: (_r, url) => {
      requests.push(url);
      return paged(url.searchParams.get("search") === "0901234567" ? [{ id: 99, bookingCode: "FOUND-OUTSIDE-FIRST-PAGE", userName: "Khách", courtName: "Sân 1", status: "Cancelled", date: "2026-10-25", slotLabel: "12:00", payableAmount: 100000 }] : []);
    } },
    { pattern: /./, respond: () => ok([]) },
  ]);
  await page.goto("/admin/bookings");
  await page.getByLabel("Tìm kiếm", { exact: true }).fill("0901234567");
  await page.getByLabel("Lọc theo ngày chơi").fill("2026-10-25");
  await expect(page.getByText("FOUND-OUTSIDE-FIRST-PAGE")).toBeVisible();
  await expect.poll(() => requests.some(u => u.searchParams.get("search") === "0901234567" && u.searchParams.get("date") === "2026-10-25" && u.searchParams.get("page") === "1")).toBe(true);
});

test("pending deadline uses server time even when the device clock is wrong", async ({ page }) => {
  await page.clock.setFixedTime(new Date("2020-01-01T00:00:00Z"));
  await seedLoggedInCustomer(page, { id: 1, fullName: "Khách", role: "Customer", phone: "0900000001" });
  await mockApi(page, [
    { pattern: /\/bookings\/77$/, respond: () => ok({ id: 77, userId: 1, courtId: 1, bookingCode: "PENDING-77", status: "Pending", courtName: "Sân 1", date: "2026-10-25", slotLabel: "12:00", playerCount: 1, payableAmount: 100000, serverTimeUtc: "2026-10-25T02:00:00Z", pendingExpiresAt: "2026-10-25T02:10:00Z" }) },
    { pattern: /./, respond: () => ok([]) },
  ]);
  await page.goto("/bookings/77");
  await expect(page.getByRole("timer")).toHaveText("Giữ chỗ còn 10:00");
  await page.clock.setFixedTime(new Date("2020-01-01T00:00:01Z"));
  await expect(page.getByRole("timer")).toHaveText("Giữ chỗ còn 9:59");
});

test("group modal traps keyboard focus and restores the trigger", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Khách", role: "Customer", phone: "0900000001" });
  await mockApi(page, [{ pattern: /./, respond: () => ok([]) }]);
  await page.goto("/groups");
  const trigger = page.getByRole("button", { name: "Tạo nhóm", exact: true });
  // Safari does not focus buttons on pointer clicks. Exercise the keyboard
  // entry path so there is an actual focused trigger to restore.
  await trigger.focus();
  await trigger.press("Enter");
  await expect(page.getByRole("dialog", { name: "Tạo nhóm mới" })).toBeVisible();
  for (let i = 0; i < 14; i++) {
    await page.keyboard.press(i % 3 === 0 ? "Shift+Tab" : "Tab");
    expect(await page.evaluate(() => !!document.activeElement?.closest('[role="dialog"]'))).toBe(true);
  }
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).toHaveCount(0);
  await expect(trigger).toBeFocused();
});

test("recovery form submits its one-time code without an old auth token", async ({ page }) => {
  let body: unknown;
  let authorization: string | undefined;
  await mockApi(page, [
    { pattern: /\/auth\/reset-password$/, method: "POST", respond: (route, _u, value) => {
      body = value; authorization = route.request().headers().authorization; return ok(null);
    } },
  ]);
  await page.goto("/auth");
  await page.getByRole("button", { name: "Quên mật khẩu?" }).click();
  await page.getByLabel("Số điện thoại khôi phục").fill("0901234567");
  await page.getByLabel("Mã khôi phục", { exact: true }).fill("A".repeat(64));
  await page.getByLabel("Mật khẩu mới", { exact: true }).fill("NewSyntheticPassword123");
  await page.getByRole("button", { name: "Đặt lại mật khẩu", exact: true }).click();
  await expect(page.getByText(/Đã đặt lại mật khẩu/)).toBeVisible();
  expect(authorization).toBeUndefined();
  expect(body).toEqual({ phoneNumber: "0901234567", code: "A".repeat(64), newPassword: "NewSyntheticPassword123" });
});

test("reviews paginate with one API request per page rather than fetching every court", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Khách", role: "Customer", phone: "0900000001" });
  const urls: URL[] = [];
  await mockApi(page, [
    { pattern: /\/reviews\/paged\?/, respond: (_r, url) => {
      urls.push(url);
      const current = Number(url.searchParams.get("page"));
      return ok({ items: [{ id: current, userName: "Khách", courtName: "Sân 1", rating: 5, comment: `Nội dung trang ${current}`, createdAt: "2026-10-25T02:00:00Z" }], totalItems: 21, totalPages: 2, currentPage: current, averageRating: 5 });
    } },
    { pattern: /./, respond: () => ok([]) },
  ]);
  await page.goto("/reviews");
  await expect(page.getByText("Nội dung trang 1")).toBeVisible();
  await page.getByRole("button", { name: "Trang sau", exact: true }).click();
  await expect(page.getByText("Nội dung trang 2")).toBeVisible();
  expect(urls.filter(u => u.searchParams.get("page") === "1")).toHaveLength(1);
  expect(urls.filter(u => u.searchParams.get("page") === "2")).toHaveLength(1);
});
