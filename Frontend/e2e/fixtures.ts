import { test as base } from "@playwright/test";

// Functional browser checks must not depend on Google Fonts availability.
// The application uses its CSS fallback font; visual font acceptance is separate.
export const test = base.extend<{ offlineFonts: void }>({
  offlineFonts: [async ({ context }, use) => {
    await context.route(/^https:\/\/fonts\.(googleapis|gstatic)\.com\//, (route) => route.abort());
    await use();
  }, { auto: true }],
});

export { expect } from "@playwright/test";
