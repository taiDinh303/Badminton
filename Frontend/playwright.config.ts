import { defineConfig, devices } from "@playwright/test";

/**
 * docs 04 §7 — E2E smoke. The app is served by `vite preview` (the built SPA).
 * Every /api/v1/** request is intercepted by Playwright with a mocked
 * BaseResponse envelope, so the suite is fully offline (no .NET runtime,
 * no SQL Server). Browsers are installed via `npx playwright install`.
 *
 *   npx playwright test
 */
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:4173",
    trace: "on-first-retry",
    ...devices["Desktop Chrome"],
  },
  webServer: {
    command: "npm run preview -- --port 4173 --strictPort",
    url: "http://localhost:4173",
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
