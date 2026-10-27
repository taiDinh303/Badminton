import { test as base } from "@playwright/test";

export const test = base.extend<{ offlineFonts: void }>({
  offlineFonts: [async ({ context }, use) => {
    await context.route(/^https:\/\/fonts\.(googleapis|gstatic)\.com\//, (route) => route.abort());
    await use();
  }, { auto: true }],
});

export { expect } from "@playwright/test";
