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
    await page.goto("/home");
    await expect(page).toHaveURL(/\/auth/);
    // Default login-mode heading (visible at desktop 1280px, unlike the
    // mobile-only "Cầu Long Booking" brand heading which is md:hidden).
    await expect(page.getByText("Chào mừng trở lại")).toBeVisible();
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
    // Disambiguate the form submit button from the "Đăng nhập" tab toggle.
    await page.locator("form").getByRole("button", { name: /Đăng nhập/i }).click();

    // After a successful login the store navigates to the home page.
    await expect(page).toHaveURL(/\/home$/);
    // HomePage renders "Xin chào," and the name in separate nodes; the name
    // also appears in the desktop sidebar, so target the hero <h1> specifically.
    await expect(page.getByRole("heading", { name: "Alice Nguyễn" })).toBeVisible();
    await expect(page.getByText("Sân Minh Long (Hà Đông)")).toBeVisible();

    await page.screenshot({ path: "test-results/desktop-customer-home.png", fullPage: true });

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
    // Disambiguate the form submit button from the "Đăng nhập" tab toggle.
    await page.locator("form").getByRole("button", { name: /Đăng nhập/i }).click();

    await expect(
      page.getByText("Sai số điện thoại hoặc mật khẩu."),
    ).toBeVisible();
  });
});
