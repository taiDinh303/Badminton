import type { Page, Route, Request } from "@playwright/test";

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

export const apiRoute = (): (url: URL) => boolean => (url) =>
  url.pathname.includes("/api/v1/");

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

export async function seedLoggedInCustomer(
  page: Page,
  user: { id: number; fullName: string; role: string; phone: string },
): Promise<void> {
  await page.addInitScript((u) => {
    localStorage.setItem("bb_token", "e2e-fake-jwt");
    sessionStorage.setItem("bb_user", JSON.stringify(u));
  }, user);
}

export function paged<T>(items: T[], totalItems = items.length, currentPage = 1) { return ok({ items, totalItems, currentPage, pageSize: 20, totalPages: Math.ceil(totalItems / 20), averageRating: items.length ? 5 : null }); }
