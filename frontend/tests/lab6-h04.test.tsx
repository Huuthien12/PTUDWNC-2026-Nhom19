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
