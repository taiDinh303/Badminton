import { test, expect } from "@playwright/test";
import { mockApi, ok, err, seedLoggedInCustomer } from "./mockApi";

test("guest can leave a contact request without signing in", async ({ page }) => {
  let submitted: unknown;
  await mockApi(page, [{ pattern: /\/contacts$/, method: "POST", respond: (_route, _url, body) => { submitted = body; return ok(null); } }]);
  await page.goto("/Home");
  const form = page.getByRole("form", { name: "Liên hệ tư vấn" });
  await form.getByLabel("Họ và tên").fill("Nguyễn An");
  await form.getByLabel("Số điện thoại").fill("090 123 4567");
  await form.getByLabel("Email").fill("an@example.com");
  await form.getByLabel("Lời nhắn").fill("Đặt sân buổi tối cho nhóm 4 người");
  await form.getByRole("checkbox").check();
  await form.getByRole("button", { name: "Gửi yêu cầu" }).click();
  await expect(page.getByRole("heading", { name: "Đã nhận lời nhắn!" })).toBeVisible();
  expect(submitted).toMatchObject({ fullName: "Nguyễn An", phone: "0901234567", email: "an@example.com", consent: true });
  expect(await page.evaluate(() => localStorage.getItem("bb_token"))).toBeNull();
});

test("invalid phone is blocked and server failure keeps entered details", async ({ page }) => {
  let attempts = 0;
  await mockApi(page, [{ pattern: /\/contacts$/, method: "POST", respond: () => { attempts++; return err(503, "UNAVAILABLE", "Unavailable"); } }]);
  await page.goto("/home#lien-he");
  const form = page.getByRole("form", { name: "Liên hệ tư vấn" });
  await form.getByLabel("Họ và tên").fill("Nguyễn An");
  await form.getByLabel("Số điện thoại").fill("123");
  await form.getByRole("checkbox").check();
  await form.getByRole("button", { name: "Gửi yêu cầu" }).click();
  await expect(page.getByRole("alert")).toContainText("hợp lệ");
  expect(attempts).toBe(0);
  await form.getByLabel("Số điện thoại").fill("0901234567");
  await form.getByRole("button", { name: "Gửi yêu cầu" }).click();
  await expect(page.getByRole("alert")).toContainText("Chưa gửi được");
  await expect(form.getByLabel("Họ và tên")).toHaveValue("Nguyễn An");
  expect(attempts).toBe(1);
});

test("admin can view a contact and mark it handled", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 1, fullName: "Admin", phone: "0900000001", role: "Admin" });
  let handled = false;
  await mockApi(page, [
    { pattern: /\/contacts\?page=1$/, method: "GET", respond: () => ok({ items: [{ id: 1, fullName: "Nguyễn An", phone: "0901234567", email: "an@example.com", message: "Đặt sân buổi tối", createdAt: "2026-10-23T12:00:00Z", handledAt: handled ? "2026-10-23T13:00:00Z" : null }], totalCount: 1 }) },
    { pattern: /\/contacts\/1\/handled$/, method: "PATCH", respond: () => { handled = true; return ok(null); } },
  ]);
  await page.goto("/admin/contacts");
  await expect(page.getByRole("heading", { name: "Nguyễn An" })).toBeVisible();
  await page.getByRole("button", { name: "Đánh dấu đã xử lý" }).click();
  await expect(page.getByText("Đã xử lý", { exact: true })).toBeVisible();
});

test("manager cannot open the global contact inbox", async ({ page }) => {
  await seedLoggedInCustomer(page, { id: 8, fullName: "Manager", phone: "0900000008", role: "Manager" });
  await mockApi(page, []);
  await page.goto("/admin/contacts");
  await expect(page).toHaveURL(/\/dashboard$/);
});
