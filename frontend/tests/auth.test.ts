import assert from "node:assert/strict";
import { beforeEach, test } from "node:test";
import { apiClient, ApiError } from "../src/services/api-client";
import { login, logout } from "../src/services/auth-service";
import {
  getSession, getValidSession, refreshSession, expireSession, subscribeSession,
  SESSION_KEY, SESSION_CHANGED, SESSION_EXPIRED,
} from "../src/services/auth-session";
import { canAccess, loginDestination } from "../src/services/auth-navigation";
import type { AuthResponse, AuthSession } from "../src/types/auth";

class MemoryStorage {
  private values = new Map<string, string>();
  getItem(key: string) { return this.values.get(key) ?? null; }
  setItem(key: string, value: string) { this.values.set(key, value); }
  removeItem(key: string) { this.values.delete(key); }
}

class Browser extends EventTarget {
  localStorage = new MemoryStorage();
}

class Locks {
  private tail: Promise<unknown> = Promise.resolve();
  request<T>(_name: string, work: () => Promise<T>): Promise<T> {
    const result = this.tail.then(work);
    this.tail = result.catch(() => {});
    return result;
  }
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => { resolve = done; });
  return { promise, resolve };
}

let browser: Browser;
let locks: Locks;
let calls: { url: string; options: RequestInit }[];
let respond: (url: string, options: RequestInit) => Promise<Response>;
let serial = 0;

function auth(version = "old", expiresAt = new Date(Date.now() + 600000).toISOString()): AuthResponse {
  return {
    accessToken: `access-${version}`, tokenType: "Bearer", refreshToken: `refresh-${version}`, expiresAt,
    user: { id: "user-1", fullName: "Author", userName: "author", email: "author@example.test", avatarUrl: null, roles: ["Author"] },
  };
}

function seed(response = auth()): AuthSession {
  const session = { ...response, sessionId: `login-${++serial}` };
  browser.localStorage.setItem(SESSION_KEY, JSON.stringify(session));
  return session;
}

beforeEach(() => {
  browser = new Browser();
  locks = new Locks();
  calls = [];
  Object.defineProperty(globalThis, "window", { value: browser, configurable: true });
  Object.defineProperty(globalThis, "navigator", { value: { locks }, configurable: true });
  respond = async () => Response.json({ ok: true });
  globalThis.fetch = async (input, options = {}) => {
    const url = String(input);
    calls.push({ url, options });
    return respond(url, options);
  };
  // Reset the storage snapshot without depending on module internals.
  getSession();
});

test("login stores full response in one entry and removes legacy token fields", async () => {
  browser.localStorage.setItem("accessToken", "legacy");
  browser.localStorage.setItem("tokenType", "Bearer");
  const response = auth();
  respond = async () => Response.json(response);
  await login({ email: "author@example.test", password: "test-only" });
  assert.deepEqual(getSession()?.user, response.user);
  assert.equal(getSession()?.expiresAt, response.expiresAt);
  assert.equal(getSession()?.refreshToken, response.refreshToken);
  assert.equal(browser.localStorage.getItem("accessToken"), null);
  assert.equal(browser.localStorage.getItem("tokenType"), null);
  assert.equal(calls.length, 1);
});

test("failed login never enters a refresh loop or creates a session", async () => {
  respond = async () => new Response(null, { status: 401 });
  await assert.rejects(login({ email: "author@example.test", password: "wrong" }), ApiError);
  assert.equal(getSession(), null);
  assert.equal(calls.length, 1);
});

test("expired requests share one rotation and all use the new bearer", async () => {
  seed(auth("old", new Date(0).toISOString()));
  const gate = deferred<Response>();
  const started = deferred<void>();
  respond = async (url, options) => {
    if (url.endsWith("/auth/refresh")) { started.resolve(); return gate.promise; }
    assert.equal(new Headers(options.headers).get("Authorization"), "Bearer access-new");
    return Response.json({ ok: true });
  };
  const requests = Array.from({ length: 5 }, () => apiClient("/protected", { auth: true }));
  await started.promise;
  const rotated = auth("new");
  rotated.user.roles = ["Admin"];
  gate.resolve(Response.json(rotated));
  await Promise.all(requests);
  assert.equal(calls.filter((c) => c.url.endsWith("/auth/refresh")).length, 1);
  assert.equal(getSession()?.refreshToken, "refresh-new");
  assert.deepEqual(getSession()?.user.roles, ["Admin"]);
});

test("401 retries the same JSON request once with the rotated bearer", async () => {
  seed();
  let attempts = 0;
  respond = async (url, options) => {
    if (url.endsWith("/auth/refresh")) return Response.json(auth("new"));
    assert.equal(options.method, "POST");
    assert.equal(options.body, '{"name":"Soup"}');
    assert.equal(new Headers(options.headers).get("X-Test"), "retained");
    return ++attempts === 1 ? new Response(null, { status: 401 }) : Response.json({ id: "created" });
  };
  assert.deepEqual(await apiClient("/protected", {
    auth: true, method: "POST", body: '{"name":"Soup"}', headers: new Headers({ "X-Test": "retained" }),
  }), { id: "created" });
  assert.equal(attempts, 2);
  assert.equal(calls.length, 3);
  assert.equal(new Headers(calls[2].options.headers).get("Authorization"), "Bearer access-new");
});

test("a late 401 reuses an already rotated pair without refreshing the old token again", async () => {
  seed();
  const late = deferred<Response>();
  const started = deferred<void>();
  let lateAttempts = 0;
  respond = async (url, options) => {
    if (url.endsWith("/auth/refresh")) return Response.json(auth("new"));
    if (url.endsWith("/late") && ++lateAttempts === 1) { started.resolve(); return late.promise; }
    return new Headers(options.headers).get("Authorization") === "Bearer access-old"
      ? new Response(null, { status: 401 }) : Response.json({ ok: true });
  };
  const slow = apiClient("/late", { auth: true });
  await started.promise;
  await apiClient("/fast", { auth: true });
  late.resolve(new Response(null, { status: 401 }));
  await slow;
  assert.equal(calls.filter((c) => c.url.endsWith("/auth/refresh")).length, 1);
});

test("second 401 clears the session, signals login, and never retries again", async () => {
  seed();
  let expired = 0;
  browser.addEventListener(SESSION_EXPIRED, () => { expired++; });
  respond = async (url) => url.endsWith("/auth/refresh")
    ? Response.json(auth("new")) : new Response(null, { status: 401 });
  await assert.rejects(apiClient("/protected", { auth: true }), ApiError);
  assert.equal(calls.length, 3);
  assert.equal(getSession(), null);
  assert.equal(expired, 1);
});

for (const failure of ["revoked", "network", "invalid-response", "expired-response"] as const) {
  test(`refresh failure (${failure}) clears state and signals login`, async () => {
    const original = seed();
    let expired = 0;
    browser.addEventListener(SESSION_EXPIRED, () => { expired++; });
    respond = async () => {
      if (failure === "network") throw new TypeError("network unavailable");
      if (failure === "expired-response") return Response.json(auth("invalid", new Date(0).toISOString()));
      return failure === "revoked" ? new Response(null, { status: 401 }) : Response.json({ accessToken: "incomplete" });
    };
    await assert.rejects(refreshSession(original));
    assert.equal(getSession(), null);
    assert.equal(expired, 1);
    assert.equal(calls.length, 1);
  });
}

test("403 never refreshes or clears an authenticated session", async () => {
  seed();
  respond = async () => new Response(null, { status: 403 });
  await assert.rejects(apiClient("/protected", { auth: true }), ApiError);
  assert.equal(calls.length, 1);
  assert.ok(getSession());
});

test("public requests and anonymous SSR do not attach credentials or refresh", async () => {
  seed(auth("old", new Date(0).toISOString()));
  await apiClient("/public");
  assert.equal(new Headers(calls[0].options.headers).has("Authorization"), false);
  assert.equal(calls.length, 1);
  Object.defineProperty(globalThis, "window", { value: undefined, configurable: true });
  assert.equal(getSession(), null);
  await apiClient("/public");
});

test("logout waits for rotation and revokes the newest pair, then stays signed out", async () => {
  const original = seed();
  const gate = deferred<Response>();
  const started = deferred<void>();
  respond = async (url, options) => {
    if (url.endsWith("/auth/refresh")) { started.resolve(); return gate.promise; }
    assert.equal(new Headers(options.headers).get("Authorization"), "Bearer access-new");
    assert.equal(options.body, JSON.stringify({ refreshToken: "refresh-new" }));
    assert.equal(getSession(), null);
    return new Response(null, { status: 204 });
  };
  const refreshing = refreshSession(original);
  await started.promise;
  const ending = logout();
  await assert.rejects(getValidSession(), ApiError);
  gate.resolve(Response.json(auth("new")));
  await Promise.all([refreshing, ending]);
  assert.equal(getSession(), null);
  assert.equal(calls.length, 2);
});

test("logout clears locally even when the server cannot confirm revocation", async () => {
  seed();
  respond = async () => { throw new TypeError("offline"); };
  await assert.rejects(logout());
  assert.equal(getSession(), null);
  assert.equal(calls.length, 1);
});

test("late refresh responses cannot restore a locally cleared session", async () => {
  const original = seed();
  const gate = deferred<Response>();
  const started = deferred<void>();
  respond = async () => { started.resolve(); return gate.promise; };
  const refreshing = refreshSession(original);
  await started.promise;
  expireSession(original);
  gate.resolve(Response.json(auth("new")));
  await assert.rejects(refreshing, ApiError);
  assert.equal(getSession(), null);
});

test("a queued tab re-reads shared storage after acquiring the lock", async () => {
  const original = seed();
  const gate = deferred<void>();
  const otherTab = locks.request("culinary.auth", async () => {
    await gate.promise;
    browser.localStorage.setItem(SESSION_KEY, JSON.stringify({ ...auth("other-tab"), sessionId: original.sessionId }));
  });
  const refreshing = refreshSession(original);
  gate.resolve();
  await otherTab;
  assert.equal((await refreshing).refreshToken, "refresh-other-tab");
  assert.equal(calls.length, 0);
});

test("old requests cannot clear or refresh a different login", async () => {
  const original = seed();
  const newer = seed(auth("different-user"));
  expireSession(original);
  await assert.rejects(refreshSession(original), ApiError);
  assert.equal(getSession()?.sessionId, newer.sessionId);
  assert.equal(calls.length, 0);
});

test("storage events and same-tab writes notify session subscribers", async () => {
  let notified = 0;
  const unsubscribe = subscribeSession(() => { notified++; });
  browser.dispatchEvent(Object.assign(new Event("storage"), { key: SESSION_KEY }));
  browser.dispatchEvent(new Event(SESSION_CHANGED));
  assert.equal(notified, 2);
  unsubscribe();
  browser.dispatchEvent(new Event(SESSION_CHANGED));
  assert.equal(notified, 2);
});

test("unsupported cross-tab locks fail closed without sending refresh", async () => {
  const original = seed();
  Object.defineProperty(globalThis, "navigator", { value: {}, configurable: true });
  await assert.rejects(refreshSession(original), ApiError);
  assert.equal(getSession(), null);
  assert.equal(calls.length, 0);
});

test("corrupt or incomplete stored sessions are treated as anonymous", async () => {
  for (const value of ["invalid JSON", JSON.stringify({ accessToken: "old" })]) {
    browser.localStorage.setItem(SESSION_KEY, value);
    assert.equal(getSession(), null);
    await assert.rejects(getValidSession(), ApiError);
    assert.equal(browser.localStorage.getItem(SESSION_KEY), null);
  }
});

test("refresh tokens stay in JSON bodies/storage, not URLs, authorization headers or errors", async () => {
  const original = seed();
  respond = async () => new Response(null, { status: 401 });
  await assert.rejects(refreshSession(original), (error: unknown) =>
    error instanceof Error && !error.message.includes(original.refreshToken));
  assert.ok(!calls[0].url.includes(original.refreshToken));
  assert.equal(new Headers(calls[0].options.headers).has("Authorization"), false);
  assert.equal(calls[0].options.body, JSON.stringify({ refreshToken: original.refreshToken }));
});

test("route roles and return destinations cannot grant Admin or redirect externally", () => {
  assert.equal(canAccess(["Author"], ["Admin"]), false);
  assert.equal(canAccess(["Admin"], ["Admin"]), true);
  assert.equal(canAccess(["Author"], ["Author", "Admin"]), true);
  assert.equal(loginDestination("/admin/categories"), "/admin/categories");
  for (const next of ["//evil.test", "https://evil.test", "javascript:alert(1)", "/admin/categories?refreshToken=secret"]) {
    assert.equal(loginDestination(next), "/");
  }
});
