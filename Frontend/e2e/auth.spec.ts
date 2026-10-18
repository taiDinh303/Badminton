import { test, expect } from "@playwright/test";
import { mockApi, ok, err, seedLoggedInCustomer } from "./mockApi";

/**
 * docs 04 §7 — auth flow. The real backend is not up; login/register and
 * every downstream GET are mocked so the test exercises the SPA's
 * authentication guard + the auth store's persistence.
 */
test.describe("auth", () => {
  test("unauthenticated visitors are redirected to /auth", async ({ page }) => {
    // No mocks: the Protected route's guard runs before any API call, so the
    // redirect happens client-side without touching the network.
    await page.goto("/");
    await expect(page).toHaveURL(/\/auth/);
    await expect(page.getByText("Cầu Lông Booking")).toBeVisible();
  });

  test("logging in persists the token and loads the home page", async ({ page }) => {
    await mockApi(page, [
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
              phone: "0900000000",
              isActive: true,
              openTime: "06:00",
              closeTime: "23:00",
              courtCount: 3,
            },
          ]),
      },
    ]);

    // Seed so the /auth route is not a hard redirect loop.
    await seedLoggedInCustomer(page, {
      id: 7,
      fullName: "Alice Nguyễn",
      role: "Customer",
      phone: "0900000007",
    });

    await page.goto("/auth");
    await page.getByLabel(/Số điện thoại/i).fill("0900000007");
    await page.getByLabel(/Mật khẩu/i).fill("Pass@123");
    await page.getByRole("button", { name: /Đăng nhập/i }).click();

    // After a successful login the store navigates to the home page.
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByText("Xin chào, Alice Nguyễn")).toBeVisible();
    await expect(page.getByText("Sân Minh Long (Hà Đông)")).toBeVisible();

    // The JWT was persisted under the app's token key.
    const token = await page.evaluate(
      () => localStorage.getItem("bb_token"),
    );
    expect(token).toBe("e2e-jwt");
  });

  test("a failed login surfaces the provider's Vietnamese error", async ({ page }) => {
    await mockApi(page, [
      {
        pattern: /\/auth\/login$/,
        method: "POST",
        respond: () => err(401, "UNAUTHENTICATED", "Sai số điện thoại hoặc mật khẩu."),
      },
    ]);

    await seedLoggedInCustomer(page, {
      id: 7,
      fullName: "Alice",
      role: "Customer",
      phone: "0900000007",
    });

    await page.goto("/auth");
    await page.getByLabel(/Số điện thoại/i).fill("0900000007");
    await page.getByLabel(/Mật khẩu/i).fill("wrong");
    await page.getByRole("button", { name: /Đăng nhập/i }).click();

    await expect(
      page.getByText("Sai số điện thoại hoặc mật khẩu."),
    ).toBeVisible();
  });
});
