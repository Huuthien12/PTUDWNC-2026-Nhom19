import { type AuthResponse, type AuthSession, isAuthResponse } from "../types/auth";
import { ApiError, httpClient } from "./http-client";

export const SESSION_KEY = "culinary.auth.v1";
export const SESSION_CHANGED = "culinary:session-changed";
export const SESSION_EXPIRED = "culinary:session-expired";
const LOCK_NAME = "culinary.auth";
let cachedValue: string | null = null;
let cachedSession: AuthSession | null = null;
let refreshFlight: { sessionId: string; promise: Promise<AuthSession> } | null = null;
let logoutFlight: Promise<void> | null = null;

export function getSession(): AuthSession | null {
  if (typeof window === "undefined") return null;
  let value: string | null;
  try { value = window.localStorage.getItem(SESSION_KEY); } catch { return null; }
  if (value === cachedValue) return cachedSession;
  cachedValue = value;
  cachedSession = null;
  try {
    const parsed: unknown = value ? JSON.parse(value) : null;
    if (isAuthResponse(parsed) && "sessionId" in parsed && typeof parsed.sessionId === "string") {
      cachedSession = parsed as AuthSession;
    }
  } catch { /* Corrupt/legacy storage is an unauthenticated session. */ }
  return cachedSession;
}

export function subscribeSession(listener: () => void) {
  const onStorage = (event: StorageEvent) => {
    if (event.key === SESSION_KEY || event.key === null) listener();
  };
  window.addEventListener("storage", onStorage);
  window.addEventListener(SESSION_CHANGED, listener);
  return () => {
    window.removeEventListener("storage", onStorage);
    window.removeEventListener(SESSION_CHANGED, listener);
  };
}

function saveSession(response: AuthResponse, sessionId: string): AuthSession {
  if (!isAuthResponse(response) || Date.parse(response.expiresAt) <= Date.now()) {
    throw new ApiError("Invalid authentication response", 502);
  }
  window.localStorage.setItem(SESSION_KEY, JSON.stringify({ ...response, sessionId }));
  window.localStorage.removeItem("accessToken");
  window.localStorage.removeItem("tokenType");
  window.dispatchEvent(new Event(SESSION_CHANGED));
  return getSession()!;
}

function clearSession() {
  try {
    window.localStorage.removeItem(SESSION_KEY);
    window.localStorage.removeItem("accessToken");
    window.localStorage.removeItem("tokenType");
  } catch { /* Storage may be unavailable in a restricted browser context. */ }
  cachedValue = null;
  cachedSession = null;
  window.dispatchEvent(new Event(SESSION_CHANGED));
}

export function expireSession(expected?: AuthSession) {
  const current = getSession();
  if (expected && (current?.sessionId !== expected.sessionId || current.refreshToken !== expected.refreshToken)) return;
  clearSession();
  window.dispatchEvent(new Event(SESSION_EXPIRED));
}

async function withSessionLock<T>(work: () => Promise<T>): Promise<T> {
  // Serialize rotation/logout/login across same-origin tabs. Fail closed without
  // Web Locks rather than racing a shared refresh token on an unsupported browser.
  if (typeof navigator === "undefined" || !navigator.locks) {
    throw new ApiError("Đăng nhập cần trình duyệt hỗ trợ Web Locks trên HTTPS hoặc localhost.", 0);
  }
  return navigator.locks.request(LOCK_NAME, work);
}

export async function establishSession(request: { email: string; password: string }): Promise<AuthResponse> {
  return withSessionLock(async () => {
    const response = await httpClient<AuthResponse>("/api/v1/auth/login", {
      method: "POST", body: JSON.stringify(request), cache: "no-store", signal: AbortSignal.timeout(15000),
    });
    return saveSession(response, crypto.randomUUID());
  });
}

export async function establishRegisteredSession(request: {
  fullName: string; email: string; userName: string; password: string;
}): Promise<AuthResponse> {
  return withSessionLock(async () => {
    const response = await httpClient<AuthResponse>("/api/v1/auth/register", {
      method: "POST", body: JSON.stringify(request), cache: "no-store", signal: AbortSignal.timeout(15000),
    });
    return saveSession(response, crypto.randomUUID());
  });
}

export function refreshSession(expected: AuthSession): Promise<AuthSession> {
  if (logoutFlight) return Promise.reject(new ApiError("Logout in progress", 401));
  if (refreshFlight?.sessionId === expected.sessionId) return refreshFlight.promise;
  const promise = withSessionLock(async () => {
    const current = getSession();
    if (!current || current.sessionId !== expected.sessionId) throw new ApiError("Session changed", 401);
    if (current.refreshToken !== expected.refreshToken) return current;
    try {
      const response = await httpClient<AuthResponse>("/api/v1/auth/refresh", {
        method: "POST", body: JSON.stringify({ refreshToken: current.refreshToken }),
        cache: "no-store", signal: AbortSignal.timeout(15000),
      });
      if (getSession()?.sessionId !== current.sessionId || getSession()?.refreshToken !== current.refreshToken) {
        throw new ApiError("Session changed", 401);
      }
      return saveSession(response, current.sessionId);
    } catch (error) {
      expireSession(current);
      throw error;
    }
  }).catch((error: unknown) => {
    expireSession(expected);
    throw error;
  }).finally(() => {
    if (refreshFlight?.promise === promise) refreshFlight = null;
  });
  refreshFlight = { sessionId: expected.sessionId, promise };
  return promise;
}

export async function getValidSession(): Promise<AuthSession> {
  if (logoutFlight) throw new ApiError("Logout in progress", 401);
  const session = getSession();
  if (!session) {
    expireSession();
    throw new ApiError("Authentication required", 401);
  }
  return Date.parse(session.expiresAt) <= Date.now() ? refreshSession(session) : session;
}

export function endSession(): Promise<void> {
  if (logoutFlight) return logoutFlight;
  const expected = getSession();
  const promise = withSessionLock(async () => {
    // Revoke the newest pair if rotation completed before acquiring the lock.
    const current = getSession();
    if (current?.sessionId !== expected?.sessionId) return;
    clearSession();
    if (current) {
      await httpClient<void>("/api/v1/auth/logout", {
        method: "POST", headers: { Authorization: `Bearer ${current.accessToken}` },
        body: JSON.stringify({ refreshToken: current.refreshToken }), cache: "no-store",
        signal: AbortSignal.timeout(15000),
      });
    }
  }).finally(() => {
    if (getSession()?.sessionId === expected?.sessionId) clearSession();
    if (logoutFlight === promise) logoutFlight = null;
  });
  logoutFlight = promise;
  return promise;
}
