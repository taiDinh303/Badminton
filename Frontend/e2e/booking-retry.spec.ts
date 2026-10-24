import { test, expect } from "@playwright/test";
import { mockApi, ok, err } from "./mockApi";

test("booking retries keep their key, while a changed payload gets a new key", async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem("bb_token", "test-token");
    sessionStorage.setItem("bb_user", JSON.stringify({ id: 1, fullName: "Retry User", phone: "0900000000", role: "Customer" }));
  });
  const attempts: { idempotencyKey: string; playerCount: number }[] = [];
  await mockApi(page, [
    { pattern: /\/bookings\/quote$/, method: "POST", respond: () => ok({ basePrice: 180000, weekendSurcharge: 0, voucherDiscount: 0, pointsDiscount: 0, total: 180000, payable: 180000 }) },
    { pattern: /\/branches$/, respond: () => ok([{ id: 1, name: "Cơ sở 1", isActive: true }]) },
    { pattern: /\/bookings\/availability\//, respond: () => ok([{
      courtId: 1, courtCode: "S01", courtName: "Sân cuối tuần",
      priceLowPeak: 120000, pricePeak: 180000,
      slots: [{ slotId: 1, label: "18:00", startTime: "18:00", endTime: "19:00", isPeak: true, isLocked: false, lockReason: "" }],
    }]) },
    { pattern: /\/bookings$/, method: "POST", respond: (_r, _u, body) => {
      attempts.push(body as typeof attempts[number]);
      return err(503, "retry", "Lỗi mạng giả lập, hãy thử lại.");
    } },
  ]);
  await page.goto("/book");
  await page.getByRole("button", { name: /18:00/ }).click();
  const dialog = page.getByRole("dialog", { name: "Xác nhận đặt sân" });
  await expect(dialog.getByText("180.000đ", { exact: true })).toBeVisible();
  const submit = dialog.getByRole("button", { name: "Xác nhận đặt sân", exact: true });
  await submit.click();
  await expect(dialog.getByText("Lỗi mạng giả lập, hãy thử lại.")).toBeVisible();
  await submit.click();
  await expect.poll(() => attempts.length).toBe(2);
  await expect(submit).toBeEnabled();
  expect(attempts[0].idempotencyKey).toMatch(/^[0-9a-f-]{36}$/);
  expect(attempts[1].idempotencyKey).toBe(attempts[0].idempotencyKey);
  await dialog.getByLabel("Số người", { exact: true }).fill("3");
  await submit.click();
  await expect.poll(() => attempts.length).toBe(3);
  expect(attempts[2].idempotencyKey).not.toBe(attempts[0].idempotencyKey);
  expect(attempts[2].playerCount).toBe(3);
});
