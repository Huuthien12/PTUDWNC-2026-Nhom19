import assert from "node:assert/strict";
import { after, test } from "node:test";
import type { ReactElement } from "react";
import { act, create, type ReactTestInstance } from "react-test-renderer";
import { AppRouterContext } from "next/dist/shared/lib/app-router-context.shared-runtime";
import AdminUsersPage from "../src/app/admin/users/page";
import RootError from "../src/app/error";
import NotFound from "../src/app/not-found";
import UnauthorizedPage from "../src/app/unauthorized/page";
import StaticUserList from "../src/components/admin/StaticUserList";
import { SESSION_KEY } from "../src/services/auth-session";

Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
const originalSelf = Object.getOwnPropertyDescriptor(globalThis, "self");
const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
const originalFetch = globalThis.fetch;
Object.defineProperty(globalThis, "self", { configurable: true, value: globalThis });
const router = { back() {}, forward() {}, refresh() {}, hmrRefresh() {}, push() {}, replace() {}, prefetch() {}, bfcacheId: "test" };

after(() => {
  if (originalSelf) Object.defineProperty(globalThis, "self", originalSelf);
  else Reflect.deleteProperty(globalThis, "self");
  if (originalWindow) Object.defineProperty(globalThis, "window", originalWindow);
  else Reflect.deleteProperty(globalThis, "window");
  globalThis.fetch = originalFetch;
});

function text(node: ReactTestInstance): string {
  return node.children.map((child) => typeof child === "string" ? child : text(child)).join("");
}

function installAdminSession() {
  const session = { sessionId: "admin", accessToken: "access", refreshToken: "refresh", tokenType: "Bearer",
    expiresAt: new Date(Date.now() + 60_000).toISOString(), user: { id: "admin", fullName: "Admin", userName: "admin",
      email: "admin@example.test", avatarUrl: null, roles: ["Admin"] } };
  Object.defineProperty(globalThis, "window", { configurable: true, value: Object.assign(new EventTarget(), {
    localStorage: { getItem: (key: string) => key === SESSION_KEY ? JSON.stringify(session) : null }, setTimeout, clearTimeout,
  }) });
}

function userPage(page = 1, totalPages = 1) {
  return Response.json({ items: [{ id: "a", userName: "anna", fullName: "Author Anna", email: "anna@example.test",
    roles: ["Author"], emailConfirmed: true, createdAt: "2026-01-01T00:00:00Z" }], totalCount: totalPages > 1 ? 13 : 1,
    page, pageSize: 12, totalPages });
}

async function render(node: ReactElement) {
  let view!: ReturnType<typeof create>;
  await act(async () => { view = create(<AppRouterContext.Provider value={router}>{node}</AppRouterContext.Provider>); });
  return view;
}

test("admin users page renders API users, controls and pagination", async () => {
  const session = { sessionId: "admin", accessToken: "access", refreshToken: "refresh", tokenType: "Bearer",
    expiresAt: new Date(Date.now() + 60_000).toISOString(), user: { id: "admin", fullName: "Admin", userName: "admin",
      email: "admin@example.test", avatarUrl: null, roles: ["Admin"] } };
  Object.defineProperty(globalThis, "window", { configurable: true, value: Object.assign(new EventTarget(), {
    localStorage: { getItem: (key: string) => key === SESSION_KEY ? JSON.stringify(session) : null }, setTimeout, clearTimeout,
  }) });
  globalThis.fetch = async (url, options) => {
    assert.match(String(url), /\/api\/v1\/admin\/users\?page=1&pageSize=12/);
    assert.equal(new Headers(options?.headers).get("Authorization"), "Bearer access");
    return Response.json({ items: [{ id: "a", userName: "admin", fullName: "Admin", email: "admin@example.test",
      roles: ["Admin"], emailConfirmed: true, createdAt: "2026-01-01T00:00:00Z" }], totalCount: 1, page: 1, pageSize: 12, totalPages: 1 });
  };
  const view = await render(<AdminUsersPage />);
  await act(async () => { await new Promise((resolve) => setImmediate(resolve)); });
  assert.match(text(view.root), /Quản lý người dùng/);
  assert.equal(view.root.findByProps({ "aria-label": "Tìm kiếm người dùng" }).props.disabled, undefined);
  assert.equal(view.root.findAllByType("tbody")[0].findAllByType("tr").length, 1);
  assert.match(text(view.root), /Trang 1/);
});

test("static user list has a presentable empty state", async () => {
  const view = await render(<StaticUserList users={[]} />);
  assert.match(text(view.root), /Chưa có người dùng/);
});

test("admin users list exposes loading and recovers from a failed request", async () => {
  installAdminSession();
  let release!: (response: Response) => void;
  let attempts = 0;
  globalThis.fetch = async () => {
    attempts++;
    if (attempts === 1) return new Promise<Response>((resolve) => { release = resolve; });
    if (attempts === 2) throw new Error("temporary API failure");
    return userPage();
  };

  const view = await render(<AdminUsersPage />);
  assert.ok(view.root.findAll((node) => node.props.role === "status").length > 0);
  await act(async () => { release(userPage()); await new Promise((resolve) => setImmediate(resolve)); });
  assert.equal(view.root.findAll((node) => node.props.role === "status").length, 0);

  await act(async () => { view.root.findByType("form").props.onSubmit({ preventDefault() {} }); await new Promise((resolve) => setImmediate(resolve)); });
  assert.ok(view.root.findAll((node) => node.props.role === "alert").length > 0);
  await act(async () => { view.root.findAllByType("button")[0].props.onClick(); await new Promise((resolve) => setImmediate(resolve)); });
  assert.match(text(view.root), /anna@example\.test/);
});

test("admin users search, role filter, and pagination send the selected query parameters", async () => {
  installAdminSession();
  const requests: URL[] = [];
  globalThis.fetch = async (input) => {
    const url = new URL(String(input), "http://frontend.test");
    requests.push(url);
    return userPage(Number(url.searchParams.get("page") ?? 1), 2);
  };

  const view = await render(<AdminUsersPage />);
  await act(async () => { await new Promise((resolve) => setImmediate(resolve)); });
  assert.equal(requests[0].searchParams.get("page"), "1");

  const search = view.root.findAllByType("input")[0];
  await act(async () => { search.props.onChange({ target: { value: "anna" } }); });
  const form = view.root.findByType("form");
  await act(async () => { form.props.onSubmit({ preventDefault() {} }); await new Promise((resolve) => setImmediate(resolve)); });
  assert.equal(requests.at(-1)?.searchParams.get("search"), "anna");

  const role = view.root.findByType("select");
  await act(async () => { role.props.onChange({ target: { value: "Author" } }); await new Promise((resolve) => setImmediate(resolve)); });
  assert.equal(requests.at(-1)?.searchParams.get("role"), "Author");

  const next = view.root.findAllByType("button").find((button) => text(button) === "Sau");
  assert.ok(next);
  await act(async () => { next!.props.onClick(); await new Promise((resolve) => setImmediate(resolve)); });
  assert.equal(requests.at(-1)?.searchParams.get("page"), "2");
});

test("unauthorized and not-found pages provide safe home navigation", async () => {
  for (const page of [<UnauthorizedPage key="unauthorized" />, <NotFound key="not-found" />]) {
    const view = await render(page);
    assert.ok(view.root.findAll((node) => node.props.href === "/").length >= 1);
  }
});

test("root error fallback retries without rendering error details", async () => {
  let retries = 0;
  const view = await render(<RootError error={new Error("private failure")} reset={() => { retries++; }} />);
  assert.doesNotMatch(text(view.root), /private failure/);
  await act(async () => { view.root.findByType("button").props.onClick(); });
  assert.equal(retries, 1);
});
