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

async function transport<T>(
  method: string,
  path: string,
  body: unknown,
  query: Record<string, string | number | boolean | undefined | null> | undefined,
  read: (response: Response) => Promise<T>,
): Promise<T> {
  const params = Object.entries(query ?? {}).filter(([, value]) => value !== undefined && value !== null).map(([key, value]) => [key, String(value)]);
  const suffix = query ? `?${new URLSearchParams(params)}` : "";
  const anonymous = ["/auth/login", "/auth/register", "/auth/reset-password"].includes(path);
  const session = beginRequest(anonymous);
  try {
    const response = await fetch(`${BASE}${path}${suffix}`, {
      method,
      headers: { ...(body instanceof FormData ? {} : { "Content-Type": "application/json" }), ...authHeaders(session.token) },
      body: body === undefined ? undefined : body instanceof FormData ? body : JSON.stringify(body),
      signal: session.signal,
    });
    if (!response.ok) {
      const error = await response.json().catch(() => null) as BaseResponse<unknown> | null;
      if (!session.isCurrent()) throw new ApiError(409, "Phiên đăng nhập đã thay đổi. Vui lòng đăng nhập lại.", "SESSION_CHANGED");
      if (response.status === 401 && session.token) expireLocalSession(true);
      throw new ApiError(response.status, error?.message ?? `HTTP ${response.status}`, error?.code ?? null);
    }
    const result = await read(response);
    if (!session.isCurrent()) throw new ApiError(409, "Phiên đăng nhập đã thay đổi. Vui lòng đăng nhập lại.", "SESSION_CHANGED");
    return result;
  } finally {
    session.release();
  }
}

export function request<T>(method: string, path: string, body?: unknown, query?: Record<string, string | number | boolean | undefined | null>): Promise<T> {
  return transport(method, path, body, query, async (response) => {
    const envelope = await response.json() as BaseResponse<T>;
    return envelope.data;
  });
}

export function requestImage(id: number): Promise<Blob> {
  return transport("GET", `/media/${id}`, undefined, undefined, async (response) => {
    if (!["image/png", "image/jpeg"].includes(response.headers.get("Content-Type")?.split(";")[0] ?? "")) throw new ApiError(502, "Máy chủ không trả về ảnh hợp lệ.");
    return response.blob();
  });
}

export function mediaResourceUrl(path: string): string {
  return /^\/api\/v1\/media\/\d+$/.test(path) ? BASE.replace(/\/api\/v1\/?$/, "") + path : path;
}
