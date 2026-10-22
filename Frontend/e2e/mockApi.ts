import type { Page, Route, Request } from "@playwright/test";

/**
 * Shared API-mock helpers for the E2E suite. The SPA is served by `vite
 * preview` on :4173 and every /api/v1/** call is intercepted here, so the
 * tests run fully offline (no .NET runtime, no SQL Server). The mock
 * envelopes mirror the real BaseResponse<T> wrapper used by lib/api.ts:
 *
 *   { data: T, message: string | null, statusCode: number, code: string | null }
 */

export type BaseEnvelope<T> = {
  data: T;
  message: string | null;
  statusCode: number;
  code: string | null;
};

export const ok = <T>(data: T): BaseEnvelope<T> => ({
  data,
  message: null,
  statusCode: 200,
  code: "success",
});

export const err = (statusCode: number, code: string, message: string): BaseEnvelope<null> => ({
  data: null,
  message,
  statusCode,
  code,
});

/** A request matcher that matches any URL whose path contains /api/v1/.
 *  Works whether the SPA calls same-origin "/api/v1/..." or an absolute
 *  http://host/api/v1/... URL. */
export const apiRoute = (): (url: URL) => boolean => (url) =>
  url.pathname.includes("/api/v1/");

/** Install a set of (regex → response) API mocks on the page. Handlers that
 *  return a Promise are awaited; returning a JSON string / object is fine. */
export async function mockApi(
  page: Page,
  handlers: Array<{
    pattern: RegExp;
    method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
    respond: (route: Route, url: URL, body: unknown) =>
      | string
      | object
      | void;
  }>,
): Promise<void> {
  await page.route(apiRoute(), async (route: Route) => {
    const req = route.request();
    const url = new URL(req.url());
    const path = url.pathname + url.search;

    for (const h of handlers) {
      if (h.method && h.method !== req.method()) continue;
      if (!h.pattern.test(path)) continue;
      const body = safeBody(req);
      const out = h.respond(route, url, body);
      if (out === undefined) continue; // let another handler / the default pass
      // Honor the BaseResponse envelope's statusCode as the real HTTP status so
      // ok/err mocks surface exactly as the SPA's api.ts expects (a non-OK
      // status makes request() throw ApiError with the envelope's message).
      let status = 200;
      if (out !== null && typeof out === "object" && typeof (out as { statusCode?: unknown }).statusCode === "number") {
        status = (out as { statusCode: number }).statusCode;
      }
      await route.fulfill({
        status,
        contentType: "application/json",
        body: typeof out === "string" ? out : JSON.stringify(out),
      });
      return;
    }
    // No handler matched — return a 404 envelope rather than letting the
    // request reach the (absent) real server.
    await route.fulfill({
      status: 404,
      contentType: "application/json",
      body: JSON.stringify(err(404, "not_found", "no mock for " + path)),
    });
  });
}

function safeBody(req: Request): unknown {
  try {
    const t = req.postData();
    if (t == null) return null;
    return JSON.parse(t);
  } catch {
    return null;
  }
}

/** Seed an authenticated customer into the app's storage so a
 *  Protected/Staff route renders without a real login. Mirrors the shape
 *  that store/auth.ts hydrates from sessionStorage (bb_user) and the JWT
 *  token (localStorage bb_token). */
export async function seedLoggedInCustomer(
  page: Page,
  user: { id: number; fullName: string; role: string; phone: string },
): Promise<void> {
  await page.addInitScript((u) => {
    localStorage.setItem("bb_token", "e2e-fake-jwt");
    sessionStorage.setItem("bb_user", JSON.stringify(u));
  }, user);
}
