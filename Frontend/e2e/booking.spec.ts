import { test, expect } from "./fixtures";
import { mockApi, ok } from "./mockApi";

/**
 * docs 04 §7 — booking flow. The availability grid + the booking drawer are
 * exercised with mocked /branches + /bookings/availability + POST /bookings.
 * All tests start from "/" and let the app's own routing / guards run, so the
 * static preview server never has to know about SPA deep routes.
 */
test.describe("booking", () => {
  test("a customer selects a free slot and confirms a booking", async ({ page }) => {
    // Unauthenticated start → the Protected guard redirects to /auth. To
    // reach /book we first complete a mocked login (mirrors a real session).

    await mockApi(page, [
    { pattern: /\/bookings\/quote$/, method: "POST", respond: () => ok({ basePrice: 100000, weekendSurcharge: 0, voucherDiscount: 0, pointsDiscount: 0, total: 100000, payable: 100000 }) },
      {
        pattern: /\/auth\/login$/,
        method: "POST",
        respond: () =>
          ok({
            userId: 7,
            fullName: "Alice Nguyễn",
            phoneNumber: "0900000007",
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
              courtCount: 2,
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
                {
                  slotId: 101,
                  label: "10:00",
                  startTime: "10:00",
                  endTime: "11:00",
                  isPeak: false,
                  isLocked: false,
                  lockReason: "",
                },
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
      {
        pattern: /\/bookings$/,
        method: "POST",
        respond: () =>
          ok({
            id: 99,
            bookingCode: "BK99999",
            courtId: 11,
            courtName: "Sân 1 (Thường)",
            date: "2026-10-24",
            slotLabel: "10:00",
            playerCount: 2,
            status: "Pending",
            totalAmount: 100000,
            discountAmount: 0,
            payableAmount: 100000,
            pointsEarned: 0,
            pointsRedeemed: 0,
            groupId: null,
            voucherRedemptionId: null,
            createdAt: "2026-10-18T00:00:00Z",
            userName: "Alice Nguyễn",
            price: {
              basePrice: 100000,
              weekendSurcharge: 0,
              voucherDiscount: 0,
              pointsDiscount: 0,
              total: 100000,
              payable: 100000,
            },
          }),
      },
    ]);

    // Complete a (mocked) login so the customer session exists.
    await page.goto("/auth");
    await page.getByLabel(/Số điện thoại/i).fill("0900000007");
    await page.getByLabel("Mật khẩu", { exact: true }).fill("Pass@123");
    // Disambiguate the form submit button from the "Đăng nhập" tab toggle.
    await page.locator("form").getByRole("button", { name: /Đăng nhập/i }).click();
    // "Xin chào," and the name are separate nodes; the name also shows in the
    // desktop sidebar, so target the hero heading specifically.
    await expect(page.getByRole("heading", { name: "Alice Nguyễn" })).toBeVisible();

    // In-app navigation to the booking grid.
    await page
      .getByRole("link", { name: "Đặt sân" })
      .filter({ visible: true })
      .first()
      .click();

    // The free 10:00 slot is clickable; the drawer opens. The court name
    // appears in both the grid card and the open drawer, so scope to the
    // confirmation drawer (the intent of this assertion).
    await page.getByRole("button", { name: /10:00/ }).click();
    await expect(
      page.getByRole("dialog", { name: "Xác nhận đặt sân" }).getByText("Sân 1 (Thường)"),
    ).toBeVisible();

    // Confirm the booking → the success card echoes the code.
    await page.getByRole("button", { name: /Xác nhận đặt sân/ }).click();
    await expect(page.getByText("BK99999")).toBeVisible();
  });
});
