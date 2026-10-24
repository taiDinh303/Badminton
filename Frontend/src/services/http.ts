import { apiBase } from "../../config/environment.mjs";
import { beginRequest, expireLocalSession } from "./session";
export { TOKEN_KEY } from "./session";
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
  const session = beginRequest(path === "/auth/login" || path === "/auth/register");
  try {
    const res = await fetch(`${BASE}${path}${qs}`, {
      method,
      headers: { "Content-Type": "application/json", ...authHeaders(session.token) },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: session.signal,
    });

    let json: BaseResponse<unknown> | null = null;
    try {
      json = (await res.json()) as BaseResponse<unknown>;
    } catch {
      /* non-JSON (rare) */
    }

    if (!session.isCurrent()) throw new ApiError(409, "Phiên đăng nhập đã thay đổi. Vui lòng đăng nhập lại.", "SESSION_CHANGED");
    if (!res.ok) {
      if (res.status === 401 && session.token) expireLocalSession(true);
      throw new ApiError(res.status, json?.message ?? `HTTP ${res.status}`, json?.code ?? null);
    }
    return json?.data as T;
  } finally {
    session.release();
  }
}
