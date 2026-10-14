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

Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
const originalSelf = Object.getOwnPropertyDescriptor(globalThis, "self");
Object.defineProperty(globalThis, "self", { configurable: true, value: globalThis });
const router = { back() {}, forward() {}, refresh() {}, hmrRefresh() {}, push() {}, replace() {}, prefetch() {}, bfcacheId: "test" };

after(() => {
  if (originalSelf) Object.defineProperty(globalThis, "self", originalSelf);
  else Reflect.deleteProperty(globalThis, "self");
});

function text(node: ReactTestInstance): string {
  return node.children.map((child) => typeof child === "string" ? child : text(child)).join("");
}

async function render(node: ReactElement) {
  let view!: ReturnType<typeof create>;
  await act(async () => { view = create(<AppRouterContext.Provider value={router}>{node}</AppRouterContext.Provider>); });
  return view;
}

test("admin users page renders presentation controls, rows and pagination", async () => {
  const view = await render(<AdminUsersPage />);
  assert.match(text(view.root), /Quản lý người dùng/);
  assert.equal(view.root.findByProps({ "aria-label": "Tìm kiếm người dùng" }).props.disabled, true);
  assert.equal(view.root.findAllByType("tbody")[0].findAllByType("tr").length, 3);
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
