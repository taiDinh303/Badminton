import { test, expect } from "@playwright/test";
import { mockApi, ok, seedLoggedInCustomer } from "./mockApi";

test("onsite mode offers Cash and QR, keeps booking pending until staff confirms", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Test User", role: "Customer", phone: "0900000000" });
  let requestedMethod = "";
  await mockApi(page, [
    { pattern: /\/payments\/methods$/, respond: () => ok(["Cash", "QR"]) },
    { pattern: /\/bookings\/42$/, respond: () => ok({ id: 42, bookingCode: "BK-ONSITE", status: "Pending", courtName: "Sân 1", date: "2099-10-21", slotLabel: "18:00", playerCount: 1, payableAmount: 100000, totalAmount: 100000, discountAmount: 0, pointsEarned: 0, pointsRedeemed: 0, createdAt: "2099-10-21T00:00:00Z" }) },
    { pattern: /\/payments\/booking\/42$/, respond: () => ok([]) },
    { pattern: /\/payments\/init$/, method: "POST", respond: (_route, _url, body) => {
      requestedMethod = (body as { method: string }).method;
      return ok({ method: "QR", gatewayUrl: null, gatewayRef: null, bookingCode: "BK-ONSITE" });
    } },
  ]);
  await page.goto("/bookings/42");
  await expect(page.getByRole("button", { name: "Tiền mặt tại sân", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "QR tại quầy", exact: true }).click();
  await expect(page.getByRole("button", { name: /VnPay|MoMo|Zalopay/ })).toHaveCount(0);
  await page.getByRole("button", { name: "Hướng dẫn thanh toán tại sân", exact: true }).click();
  await expect(page.getByText(/Đơn chỉ được xác nhận sau khi nhân viên/)).toBeVisible();
  expect(requestedMethod).toBe("QR");
  await expect(page.getByText("Chờ thanh toán", { exact: true })).toBeVisible();
});
