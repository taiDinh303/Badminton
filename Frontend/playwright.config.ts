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
  // Optional before/after desktop comparison; generated images stay outside Git.
  snapshotPathTemplate: process.env.VERIFY_DESKTOP_BASELINE
    ? "../artifacts/responsive-desktop-baseline/{arg}{ext}"
    : undefined,
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://127.0.0.1:4173",
    trace: "on-first-retry",
    ...devices["Desktop Chrome"],
  },
  webServer: {
    command: "npm run preview -- --host 127.0.0.1 --port 4173 --strictPort",
    url: "http://127.0.0.1:4173",
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
