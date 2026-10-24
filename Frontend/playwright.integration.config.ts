import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./integration",
  workers: 1,
  retries: 0,
  forbidOnly: !!process.env.CI,
  reporter: "list",
  use: { baseURL: "http://127.0.0.1:5099", ...devices["Desktop Chrome"], trace: "retain-on-failure" },
  webServer: {
    cwd: "..",
    command: "dotnet run --project Backend/BrowserTests/Badminton.BrowserTestHost.csproj -c Release --no-launch-profile",
    url: "http://127.0.0.1:5099/health",
    reuseExistingServer: false,
    timeout: 120_000,
  },
});
