export function apiBase(value = "") {
  const input = String(value ?? "").trim().replace(/\/+$/, "");
  if (!input || input === "/api/v1") return "/api/v1";
  let url;
  try { url = new URL(input); } catch { throw new Error("CONFIGURATION_INVALID: VITE_API_URL must end in /api/v1."); }
  const local = ["localhost", "127.0.0.1", "[::1]"].includes(url.hostname);
  if ((url.protocol !== "https:" && !(local && url.protocol === "http:")) || url.username || url.password
      || url.search || url.hash || url.pathname !== "/api/v1")
    throw new Error("CONFIGURATION_INVALID: VITE_API_URL requires HTTPS (HTTP loopback only), /api/v1 and no credentials/query/fragment.");
  return url.href.replace(/\/+$/, "");
}

export function proxyTarget(value = "http://localhost:5131") {
  let url;
  try { url = new URL(value || "http://localhost:5131"); } catch { throw new Error("CONFIGURATION_INVALID: API_PROXY_TARGET is invalid."); }
  if (!["http:", "https:"].includes(url.protocol) || url.username || url.password || url.pathname !== "/" || url.search || url.hash)
    throw new Error("CONFIGURATION_INVALID: API_PROXY_TARGET requires a server origin.");
  return url.origin;
}
