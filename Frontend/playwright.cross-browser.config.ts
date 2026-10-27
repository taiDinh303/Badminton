import { defineConfig, devices } from "@playwright/test";
import base from "./playwright.config";

export default defineConfig({
  ...base,
  testMatch: ["auth.spec.ts", "booking.spec.ts", "session-isolation.spec.ts", "nonpayment-hardening.spec.ts", "booking-experience.spec.ts"],
  projects: [
    { name: "firefox", use: { ...devices["Desktop Firefox"] } },
    { name: "webkit", use: { ...devices["Desktop Safari"] } },
  ],
});
