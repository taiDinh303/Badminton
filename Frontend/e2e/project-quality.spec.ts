import { test, expect } from "@playwright/test";
import { mockApi, ok, err, seedLoggedInCustomer } from "./mockApi";

test("admin creates a branch and edits its address through the UI", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Admin", phone: "0900000001", role: "Admin" });
  let branch: { id: number; name: string; address: string; openTime: string; closeTime: string; isActive: boolean } | null = null;
  await mockApi(page, [
    { pattern: /\/branches$/, method: "GET", respond: () => ok(branch ? [branch] : []) },
    { pattern: /\/branches$/, method: "POST", respond: (_r, _u, body) => { branch = { ...(body as NonNullable<typeof branch>), id: 1, isActive: true }; return ok(1); } },
    { pattern: /\/branches\/1$/, method: "PATCH", respond: (_r, _u, body) => { branch = { ...branch!, ...(body as object) }; return ok(null); } },
  ]);
  await page.goto("/admin/branches");
  await page.getByRole("button", { name: "Thêm cơ sở", exact: true }).click();
  const modal = page.getByRole("dialog");
  await modal.getByLabel("Tên cơ sở", { exact: true }).fill("Cơ sở mới");
  await modal.getByLabel("Địa chỉ", { exact: true }).fill("Địa chỉ cũ");
  await modal.getByRole("button", { name: "Lưu cơ sở", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Cơ sở mới", exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Sửa Cơ sở mới", exact: true }).click();
  await modal.getByLabel("Địa chỉ", { exact: true }).fill("Địa chỉ mới");
  await modal.getByRole("button", { name: "Lưu cơ sở", exact: true }).click();
  await expect(page.getByText("Địa chỉ mới", { exact: true })).toBeVisible();
});

test("slot price stays fixed, discounts use server quote, invalid vouchers block submission", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Customer", phone: "0900000001", role: "Customer" });
  let submissions = 0;
  await mockApi(page, [
    { pattern: /\/branches$/, respond: () => ok([{ id: 1, name: "Branch", isActive: true }]) },
    { pattern: /\/bookings\/availability\//, respond: () => ok([{
      courtId: 1, courtName: "Court", priceLowPeak: 100000, pricePeak: 150000, capacity: 2,
      slots: [{ slotId: 1, label: "12:00", startTime: "12:00", endTime: "13:00", isPeak: false, isLocked: false }],
    }]) },
    { pattern: /\/bookings\/quote$/, method: "POST", respond: (_r, _u, body) => {
      const request = body as { voucherCode?: string; pointsToRedeem?: number };
      if (request.voucherCode === "INVALID") return err(400, "invalid", "Voucher không hợp lệ.");
      const voucherDiscount = request.voucherCode === "SAVE" ? 10000 : 0;
      const pointsDiscount = request.pointsToRedeem === 1000 ? 20000 : 0;
      return ok({ basePrice: 100000, weekendSurcharge: 0, total: 100000, payable: 100000 - voucherDiscount - pointsDiscount, voucherDiscount, pointsDiscount });
    } },
    { pattern: /\/bookings$/, method: "POST", respond: () => { submissions++; return err(503, "retry", "fixture"); } },
  ]);
  await page.goto("/book");
  await page.getByRole("button", { name: /12:00/ }).click();
  const dialog = page.getByRole("dialog", { name: "Xác nhận đặt sân" });
  const submit = dialog.getByRole("button", { name: "Xác nhận đặt sân", exact: true });
  await expect(submit).toBeEnabled();
  await dialog.getByLabel("Số người", { exact: true }).fill("1");
  await expect(submit).toBeEnabled();
  await expect(dialog.getByText("100.000đ", { exact: true })).toBeVisible();
  await dialog.getByLabel("Số người", { exact: true }).fill("9");
  await expect(dialog.getByLabel("Số người", { exact: true })).toHaveValue("2");
  await dialog.getByLabel("Mã voucher").fill("SAVE");
  await dialog.getByLabel("Số điểm đổi").fill("1000");
  await expect(dialog.getByText("70.000đ", { exact: true })).toBeVisible();
  await dialog.getByLabel("Mã voucher").fill("INVALID");
  await expect(dialog.getByText("Voucher không hợp lệ.")).toBeVisible();
  await expect(submit).toBeDisabled();
  expect(submissions).toBe(0);
});

test("customer can cancel a confirmed booking from its detail page", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Customer", phone: "0900000001", role: "Customer" });
  let status = "Confirmed";
  await mockApi(page, [
    { pattern: /\/bookings\/1$/, respond: () => ok({ id: 1, userId: 1, courtId: 1, courtName: "Court", bookingCode: "BK1", date: "2026-10-29", slotLabel: "12:00", playerCount: 2, status }) },
    { pattern: /\/bookings\/1\/cancel$/, method: "POST", respond: () => { status = "Cancelled"; return ok({ id: 1, status }); } },
    { pattern: /\/payments\/booking\/1$/, respond: () => ok([]) },
  ]);
  await page.goto("/bookings/1");
  await page.getByRole("button", { name: "Huỷ đặt chỗ", exact: true }).click();
  await expect.poll(() => status).toBe("Cancelled");
  await expect(page.getByRole("button", { name: "Huỷ đặt chỗ", exact: true })).toHaveCount(0);
});

test("admin can reactivate an inactive slot", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Admin", phone: "0900000001", role: "Admin" });
  let active = false;
  let includeInactive = false;
  await mockApi(page, [
    { pattern: /\/time-slots\?/, method: "GET", respond: (_r, u) => {
      includeInactive = u.searchParams.get("includeInactive") === "true";
      return ok([{ id: 1, label: "12:00", startTime: "12:00", endTime: "13:00", isActive: active, isPeak: false }]);
    } },
    { pattern: /\/time-slots\/1$/, method: "PATCH", respond: (_r, _u, b) => { active = (b as { isActive: boolean }).isActive; return ok(null); } },
  ]);
  await page.goto("/admin/slots");
  await page.getByRole("button", { name: "Sửa", exact: true }).click();
  expect(includeInactive).toBe(true);
  await page.getByLabel("Đang hoạt động", { exact: true }).check();
  await page.getByRole("button", { name: "Lưu", exact: true }).click();
  await expect.poll(() => active).toBe(true);

});

test("staff creation requires a branch and offers only Manager", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Admin", phone: "0900000001", role: "Admin" });
  await mockApi(page, [
    { pattern: /\/branches$/, respond: () => ok([{ id: 1, name: "Branch", isActive: true }]) },
    { pattern: /\/admin\/staff$/, method: "GET", respond: () => ok([]) },
  ]);
  await page.goto("/admin/staff");
  await page.getByRole("button", { name: "Tạo nhân viên", exact: true }).click();
  const modal = page.getByRole("dialog");
  await expect(modal.getByLabel("Vai trò").locator("option")).toHaveCount(1);
  await expect(modal.getByRole("button", { name: "Tạo nhân viên", exact: true })).toBeDisabled();
  await expect(modal.getByLabel("Cơ sở (bắt buộc)")).toBeVisible();
});
