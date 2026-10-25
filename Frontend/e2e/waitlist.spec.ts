import { test, expect } from "@playwright/test";
import { mockApi, ok, err, paged } from "./mockApi";

/**
 * docs 04 §7 — waitlist flow (FEAT 2). In BookPage a slot cell is only
 * clickable when it is free; the "Tham gia danh sách chờ" CTA appears in the
 * confirmation drawer AFTER a booking create fails (e.g. the slot was taken
 * between the availability fetch and the confirm → POST /bookings 409). The
 * suite reproduces exactly that race with a mocked 409, then manages the
 * entry from the /waitlist page.
 */
test.describe("waitlist", () => {
  test("join after a create conflict, then leave the entry", async ({ page }) => {
    let joinBody: unknown = null;
    let left = false;

    await mockApi(page, [
    { pattern: /\/bookings\/quote$/, method: "POST", respond: () => ok({ basePrice: 100000, weekendSurcharge: 0, voucherDiscount: 0, pointsDiscount: 0, total: 100000, payable: 100000 }) },
      {
        pattern: /\/auth\/login$/,
        method: "POST",
        respond: () =>
          ok({
            userId: 8,
            fullName: "Bob Trần",
            phoneNumber: "0900000008",
            role: "Customer",
            accessToken: "e2e-jwt",
            expiresInMinutes: 60,
          }),
      },
      {
        pattern: /\/branches$/,
        method: "GET",
        respond: () =>
          ok([
            {
              id: 1,
              name: "Sân Minh Long (Hà Đông)",
              address: "123 Nguyễn Văn A",
              isActive: true,
              openTime: "06:00",
              closeTime: "23:00",
            },
          ]),
      },
      {
        pattern: /\/bookings\/availability\/\d+\/\d{4}-\d{2}-\d{2}$/,
        method: "GET",
        respond: () =>
          ok([
            {
              courtId: 11,
              courtCode: "S01",
              courtName: "Sân 1 (Thường)",
              priceLowPeak: 100000,
              pricePeak: 150000,
              slots: [
                // A free slot so the drawer opens; the create will 409.
                {
                  slotId: 102,
                  label: "18:00",
                  startTime: "18:00",
                  endTime: "19:00",
                  isPeak: true,
                  isLocked: false,
                  lockReason: "",
                },
              ],
            },
          ]),
      },
      // The booking "loses the race" → 409 SLOT_FULL.
      {
        pattern: /\/bookings$/,
        method: "POST",
        respond: () => err(409, "SLOT_FULL", "Khung giờ này đã được đặt hết."),
      },
      {
        pattern: /\/waitlist\/join$/,
        method: "POST",
        respond: (_route, _url, body) => {
          joinBody = body;
          return ok({ id: 501, status: "Waiting" });
        },
      },
      {
        pattern: /\/waitlist\/mine\/paged\?/,
        method: "GET",
        respond: () =>
          left
            ? paged([])
            : paged([
                {
                  id: 501,
                  courtId: 11,
                  courtName: "Sân 1 (Thường)",
                  slotLabel: "18:00",
                  date: "2026-10-24",
                  playerCount: 2,
                  status: "Waiting",
                  createdAt: "2026-10-18T00:00:00Z",
                  promotedBookingId: null,
                },
              ]),
      },
      {
        pattern: /\/waitlist\/\d+\/leave$/,
        method: "POST",
        respond: () => {
          left = true;
          return ok(null);
        },
      },
    ]);

    // Log in (mocked) → home.
    await page.goto("/auth");
    await page.getByLabel(/Số điện thoại/i).fill("0900000008");
    await page.getByLabel("Mật khẩu", { exact: true }).fill("Pass@123");
    // Disambiguate the form submit button from the "Đăng nhập" tab toggle.
    await page.locator("form").getByRole("button", { name: /Đăng nhập/i }).click();
    // "Xin chào," and the name are separate nodes; the name also shows in the
    // desktop sidebar, so target the hero heading specifically.
    await expect(page.getByRole("heading", { name: "Bob Trần" })).toBeVisible();

    // Open the booking grid (desktop sidebar; mobile twin is hidden at 1280px).
    await page
      .getByRole("link", { name: "Đặt sân" })
      .filter({ visible: true })
      .first()
      .click();

    // Open the drawer on the free peak slot, confirm → the mocked 409 makes
    // the create fail, surfacing the FEAT-2 join CTA.
    await page.getByRole("button", { name: /18:00/ }).click();
    await page.getByRole("button", { name: /Xác nhận đặt sân/ }).click();
    await expect(page.getByText(/đã được đặt hết/)).toBeVisible();
    await page.getByRole("button", { name: /Tham gia danh sách chờ/ }).click();
    await expect(page.getByText(/Đã xếp bạn vào danh sách chờ/)).toBeVisible();
    expect(typeof (joinBody as { playerCount?: number } | null)?.playerCount).toBe(
      "number",
    );

    // Manage the entry from the waitlist page.
    await page
      .getByRole("link", { name: "Chờ" })
      .filter({ visible: true })
      .first()
      .click();
    await expect(page.getByText("Sân 1 (Thường) · 18:00")).toBeVisible();
    await page.getByRole("button", { name: /Rời danh sách/ }).click();
    // The leave mutation invalidates /waitlist/mine; the mock now returns [].
    await expect(page.getByText(/Bạn chưa nằm trong danh sách chờ nào/)).toBeVisible();
  });
});
