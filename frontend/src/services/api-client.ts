import { expireSession, getSession, getValidSession, refreshSession } from "./auth-session";
import { ApiError, httpClient } from "./http-client";
import type { AuthSession } from "../types/auth";

export { ApiError } from "./http-client";
export type ApiOptions = RequestInit & { auth?: boolean };

export async function apiClient<T>(endpoint: string, options: ApiOptions = {}): Promise<T> {
  const { auth = false, ...request } = options;
  if (!auth) return httpClient<T>(endpoint, request);
  const session = await getValidSession();
  const send = (current: AuthSession) => {
    const headers = new Headers(request.headers);
    headers.set("Authorization", `Bearer ${current.accessToken}`);
    return httpClient<T>(endpoint, { ...request, headers, cache: "no-store" });
  };
  try {
    return await send(session);
  } catch (error) {
    if (!(error instanceof ApiError) || error.status !== 401) throw error;
    if (getSession()?.sessionId !== session.sessionId) throw error;
    const refreshed = await refreshSession(session);
    try {
      // At most one retry. 403/network/business errors never trigger refresh.
      return await send(refreshed);
    } catch (retryError) {
      if (retryError instanceof ApiError && retryError.status === 401) expireSession(refreshed);
      throw retryError;
    }
  }
}
