import { apiBase } from "../../config/environment.mjs";
const BASE = apiBase(import.meta.env.VITE_API_URL);

export class ApiError extends Error {
  readonly status: number;
  readonly code: string | null;
  constructor(status: number, message: string, code: string | null = null) {
    super(message);
    this.status = status;
    this.code = code;
  }
}

interface BaseResponse<T> {
  data: T;
  message: string | null;
  statusCode: number;
  code: string | null;
}

export const TOKEN_KEY = "bb_token";

function authHeaders(token: string | null): Record<string, string> {
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  query?: Record<string, string | number | boolean | undefined | null>,
): Promise<T> {
  const qs = query
    ? `?${new URLSearchParams(
        Object.entries(query)
          .filter(([, v]) => v !== undefined && v !== null)
          .map(([k, v]) => [k, String(v)]),
      ).toString()}`
    : "";
  const requestToken = localStorage.getItem(TOKEN_KEY);
  const res = await fetch(`${BASE}${path}${qs}`, {
    method,
    headers: { "Content-Type": "application/json", ...authHeaders(requestToken) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  let json: BaseResponse<unknown> | null = null;
  try {
    json = (await res.json()) as BaseResponse<unknown>;
  } catch {
    /* non-JSON (rare) */
  }

  if (!res.ok) {
    if (res.status === 401 && requestToken && localStorage.getItem(TOKEN_KEY) === requestToken) {
      localStorage.removeItem(TOKEN_KEY);
      sessionStorage.removeItem("bb_user");
      window.dispatchEvent(new Event("badminton:session-expired"));
    }
    throw new ApiError(res.status, json?.message ?? `HTTP ${res.status}`, json?.code ?? null);
  }
  return json?.data as T;
}
