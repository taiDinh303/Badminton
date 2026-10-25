import { test, expect } from "./fixtures";
import type { BrowserContext, Page } from "@playwright/test";
import { ok } from "./mockApi";

async function mockSessions(context: BrowserContext) {
  await context.route("**/api/v1/**", async (route) => {
    const request = route.request();
    const path = new URL(request.url()).pathname;
    let data: unknown = [];
    if (path === "/api/v1/auth/login") {
      const bob = request.postDataJSON().phoneNumber === "0900000002";
      data = { userId: bob ? 2 : 1, fullName: bob ? "Bob" : "Alice", role: "Customer",
        phoneNumber: bob ? "0900000002" : "0900000001", accessToken: bob ? "token-bob" : "token-alice", expiresInMinutes: 60 };
    }
    if (path === "/api/v1/bookings/mine") data = { items: [], totalCount: 0 };
    if (path === "/api/v1/account/summary") data = { pointsBalance: 1234 };
    if (path === "/api/v1/account/profile") {
      const bob = request.headers().authorization === "Bearer token-bob";
      data = { id: bob ? 2 : 1, fullName: bob ? "Bob" : "Alice", tier: "None", multiplier: 1 };
    }
    await route.fulfill({ json: ok(data) });
  });
}

async function login(page: Page, phone = "0900000001") {
  await page.goto("/auth");
  await page.getByLabel("Số điện thoại", { exact: true }).fill(phone);
  await page.getByLabel("Mật khẩu", { exact: true }).fill("BrowserOnly$2026");
  await page.locator("form").getByRole("button", { name: "Đăng nhập", exact: true }).click();
  // Login can restore /account after another tab expired this tab's session.
  await expect(page.getByText(phone.endsWith("2") ? "Bob" : "Alice", { exact: true }).filter({ visible: true }).first()).toBeVisible();
}

test("changing account in another tab signs the old tab out without erasing the new token", async ({ context, page }) => {
  await mockSessions(context);
  await login(page);
  await page.goto("/account");
  await expect(page.getByText(/^1\.234\s*điểm$/)).toBeVisible();
  const second = await context.newPage();
  await login(second, "0900000002");
  await expect(page).toHaveURL(/\/auth$/);
  expect(await page.evaluate(() => sessionStorage.getItem("bb_user"))).toBeNull();
  expect(await second.evaluate(() => localStorage.getItem("bb_token"))).toBe("token-bob");
  await login(page);
  await expect(second).toHaveURL(/\/auth$/);
  expect(await page.evaluate(() => localStorage.getItem("bb_token"))).toBe("token-alice");
  await page.goto("/account");
  await page.getByRole("button", { name: "Đăng xuất", exact: true }).last().click();
  await expect(page).toHaveURL(/\/auth$/);
  expect(await page.evaluate(() => localStorage.getItem("bb_token"))).toBeNull();
});

test("a missed storage event cannot make old identity requests use a replacement token", async ({ context, page }) => {
  await mockSessions(context);
  await login(page);
  // Populate both account queries before changing the shared token. Navigation
  // must reject the old identity even when fresh cache data avoids all HTTP.
  await page.getByRole("link", { name: "Tài khoản", exact: true }).filter({ visible: true }).first().click();
  await expect(page.getByText(/^1\.234\s*điểm$/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Chỉnh sửa tài khoản", exact: true })).toBeEnabled();
  await page.getByRole("link", { name: "Trang chính", exact: true }).filter({ visible: true }).first().click();
  await expect(page.getByRole("heading", { name: "Alice", exact: true })).toBeVisible();
  const wrongIdentityRequests: string[] = [];
  page.on("request", request => {
    if (request.url().includes("/api/v1/") && request.headers().authorization === "Bearer token-bob")
      wrongIdentityRequests.push(request.url());
  });
  // Same-tab writes do not fire a storage event. The transport must still
  // detect the mismatch before issuing a request; reload must also reject it.
  await page.evaluate(() => localStorage.setItem("bb_token", "token-bob"));
  await page.getByRole("link", { name: "Tài khoản", exact: true }).filter({ visible: true }).first().click();
  await expect(page).toHaveURL(/\/auth$/);
  await expect.poll(() => page.evaluate(() => sessionStorage.getItem("bb_user"))).toBeNull();
  expect(wrongIdentityRequests).toEqual([]);
  expect(await page.evaluate(() => localStorage.getItem("bb_token"))).toBe("token-bob");
  await page.reload();
  await expect(page).toHaveURL(/\/auth$/);
});

test("logging out in one tab clears another tab with the same session", async ({ context, page }) => {
  await mockSessions(context);
  await login(page);
  const second = await context.newPage();
  await login(second);
  await page.goto("/account");
  await page.getByRole("button", { name: "Đăng xuất", exact: true }).last().click();
  await expect(page).toHaveURL(/\/auth$/);
  await expect(second).toHaveURL(/\/auth$/);
  expect(await second.evaluate(() => sessionStorage.getItem("bb_user"))).toBeNull();
});

test("reload rejects identity bound to a different shared token", async ({ context, page }) => {
  await mockSessions(context);
  await login(page);
  await page.evaluate(() => localStorage.setItem("bb_token", "token-bob"));
  await page.reload();
  await expect(page).toHaveURL(/\/auth$/);
  expect(await page.evaluate(() => sessionStorage.getItem("bb_user"))).toBeNull();
  expect(await page.evaluate(() => localStorage.getItem("bb_token"))).toBe("token-bob");
});

test("a late profile mutation from the old account cannot rename the new account", async ({ context, page }) => {
  await mockSessions(context);
  await login(page);
  let release!: () => void;
  const delayed = new Promise<void>(resolve => { release = resolve; });
  let responded!: () => void;
  const finished = new Promise<void>(resolve => { responded = resolve; });
  await page.route("**/api/v1/account/profile", async route => {
    if (route.request().method() !== "PATCH") return route.fallback();
    await delayed;
    try { await route.fulfill({ json: ok({ id: 1, fullName: "Old Alice response" }) }); }
    finally { responded(); }
  });
  await page.goto("/account");
  await page.getByRole("button", { name: "Chỉnh sửa tài khoản", exact: true }).click();
  await page.getByLabel("Họ và tên", { exact: true }).fill("Old Alice response");
  const submitted = page.waitForRequest(request => request.url().endsWith("/account/profile") && request.method() === "PATCH");
  await page.getByRole("button", { name: "Lưu tài khoản", exact: true }).click();
  await submitted;
  // Navigate within the SPA so the pending mutation remains in memory.
  await page.evaluate(() => { history.pushState(null, "", "/auth"); window.dispatchEvent(new PopStateEvent("popstate")); });
  await page.getByLabel("Số điện thoại", { exact: true }).fill("0900000002");
  await page.getByLabel("Mật khẩu", { exact: true }).fill("BrowserOnly$2026");
  await page.locator("form").getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Bob", exact: true })).toBeVisible();
  release();
  await finished;
  await expect(page.getByRole("heading", { name: "Bob", exact: true })).toBeVisible();
  expect(await page.evaluate(() => JSON.parse(sessionStorage.getItem("bb_user")!).fullName)).toBe("Bob");
});
