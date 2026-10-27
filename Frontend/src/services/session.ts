export const TOKEN_KEY = "bb_token";
const BOUND_TOKEN_KEY = "bb_session_token";

let token = sessionStorage.getItem("bb_user")
  ? sessionStorage.getItem(BOUND_TOKEN_KEY) ?? localStorage.getItem(TOKEN_KEY)
  : null;
if (token) sessionStorage.setItem(BOUND_TOKEN_KEY, token);
let generation = 0;
const requests = new Set<AbortController>();

function cancelRequests() {
  generation++;
  for (const controller of requests) controller.abort();
  requests.clear();
}

export function startSession(accessToken: string) {
  cancelRequests();
  token = accessToken;
  sessionStorage.setItem(BOUND_TOKEN_KEY, accessToken);
  localStorage.setItem(TOKEN_KEY, accessToken);
}

export function endSession(removeSharedToken = true) {
  // A stale tab must never erase the new account's shared token.
  if (removeSharedToken && token && localStorage.getItem(TOKEN_KEY) === token)
    localStorage.removeItem(TOKEN_KEY);
  token = null;
  sessionStorage.removeItem(BOUND_TOKEN_KEY);
  sessionStorage.removeItem("bb_user");
  cancelRequests();
}

export function sessionMatchesStorage() {
  return token === null || token === localStorage.getItem(TOKEN_KEY);
}

export function expireLocalSession(removeSharedToken = false) {
  endSession(removeSharedToken);
  window.dispatchEvent(new Event("badminton:session-expired"));
}

export function beginRequest(anonymous = false) {
  if (!sessionMatchesStorage()) {
    expireLocalSession();
    if (!anonymous) throw new DOMException("Phiên đăng nhập đã thay đổi. Vui lòng đăng nhập lại.", "AbortError");
  }
  const controller = new AbortController();
  requests.add(controller);
  const version = generation;
  return {
    token: anonymous ? null : token,
    signal: controller.signal,
    isCurrent: () => version === generation && sessionMatchesStorage(),
    release: () => requests.delete(controller),
  };
}
