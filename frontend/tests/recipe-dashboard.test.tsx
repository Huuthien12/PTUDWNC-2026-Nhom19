import assert from "node:assert/strict";
import { afterEach, beforeEach, test } from "node:test";
import { act, create, type ReactTestInstance, type ReactTestRenderer } from "react-test-renderer";
import { AppRouterContext } from "next/dist/shared/lib/app-router-context.shared-runtime";
import AuthControls from "../src/components/auth/AuthControls";
import AuthorRecipeDashboard from "../src/components/recipes/AuthorRecipeDashboard";
import { SESSION_KEY } from "../src/services/auth-session";
import type { RecipeDashboardItem, RecipeDashboardPage } from "../src/types/recipe";

Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
const originalFetch = globalThis.fetch;
const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
const originalSelf = Object.getOwnPropertyDescriptor(globalThis, "self");
let view: ReactTestRenderer | undefined;
let confirmResult = true;
let sessionRoles = ["Author"];
let requests: { path: string; method: string; body: Record<string, unknown> }[];
let responder: (path: string, method: string, body: Record<string, unknown>) => Promise<Response>;

const item = (id: string, status: 1 | 2 | 3 = 1, rowVersion = "v1"): RecipeDashboardItem => ({
  id, title: `Recipe ${id}`, slug: `recipe-${id}`, status, rowVersion, categoryName: "Món Việt",
  thumbnailUrl: null, publishedAt: status === 2 ? "2026-01-01T00:00:00Z" : null,
  createdAt: "2026-01-01T00:00:00Z", updatedAt: "2026-01-01T00:00:00Z",
});
const page = (pageNumber: number, items: RecipeDashboardItem[], totalPages = 1): RecipeDashboardPage => ({
  items, page: pageNumber, pageSize: 12, totalPages, totalCount: totalPages ? totalPages * 12 : 0,
});

beforeEach(() => {
  Object.defineProperty(globalThis, "self", { configurable: true, value: globalThis });
  sessionRoles = ["Author"];
  const session = { sessionId: "dashboard", accessToken: "access", refreshToken: "refresh", tokenType: "Bearer",
    expiresAt: new Date(Date.now() + 600000).toISOString(), user: { id: "author", fullName: "Author",
      userName: "author", email: "a@example.test", avatarUrl: null, roles: sessionRoles } };
  Object.defineProperty(globalThis, "window", { configurable: true, value: Object.assign(new EventTarget(), {
    confirm: () => confirmResult,
    setTimeout: globalThis.setTimeout,
    clearTimeout: globalThis.clearTimeout,
    localStorage: { getItem: (key: string) => key === SESSION_KEY ? JSON.stringify(session) : null },
  }) });
  confirmResult = true;
  requests = [];
  responder = async () => Response.json(page(1, [item("one")], 1));
  globalThis.fetch = async (url, options = {}) => {
    const parsed = new URL(String(url), "http://localhost");
    const method = options.method ?? "GET";
    const body = options.body ? JSON.parse(String(options.body)) : {};
    requests.push({ path: `${parsed.pathname}${parsed.search}`, method, body });
    return responder(`${parsed.pathname}${parsed.search}`, method, body);
  };
});

afterEach(async () => {
  if (view) await act(async () => { view!.unmount(); });
  view = undefined;
  globalThis.fetch = originalFetch;
  if (originalWindow) Object.defineProperty(globalThis, "window", originalWindow);
  else Reflect.deleteProperty(globalThis, "window");
  if (originalSelf) Object.defineProperty(globalThis, "self", originalSelf);
  else Reflect.deleteProperty(globalThis, "self");
});

async function flush(action?: () => void) {
  await act(async () => {
    action?.();
    for (let index = 0; index < 5; index++)
      await new Promise<void>((resolve) => setTimeout(resolve, 10));
  });
}
async function mount() {
  await flush(() => { view = create(<AuthorRecipeDashboard />); });
  await flush(() => window.dispatchEvent(new Event("culinary:session-changed")));
}
function text(node: ReactTestInstance): string {
  return node.children.map((child) => typeof child === "string" ? child : text(child)).join("");
}
function button(label: string) {
  return view!.root.findAllByType("button").find((node) => text(node) === label)!;
}

test("dashboard paginates through mine results and disables first/last controls", async () => {
  responder = async (path) => path.includes("page=2")
    ? Response.json(page(2, [item("two", 2)], 2))
    : Response.json(page(1, [item("one")], 2));
  await mount();
  assert.match(text(view!.root), /Recipe one/);
  assert.equal(button("Trang trước").props.disabled, true);
  await flush(() => button("Trang sau").props.onClick());
  assert.match(text(view!.root), /Recipe two/);
  assert.equal(button("Trang sau").props.disabled, true);
  await flush(() => button("Trang trước").props.onClick());
  assert.match(text(view!.root), /Recipe one/);
  assert.deepEqual(requests.filter((request) => request.method === "GET").map((request) => request.path), [
    "/api/recipe-cache/api/v1/recipes/mine?page=1&pageSize=12",
    "/api/recipe-cache/api/v1/recipes/mine?page=2&pageSize=12",
    "/api/recipe-cache/api/v1/recipes/mine?page=1&pageSize=12",
  ]);
});

test("dashboard renders a real empty state and recovers from a load error", async () => {
  let fails = true;
  responder = async () => fails ? new Response(null, { status: 500 }) : Response.json(page(1, [], 0));
  await mount();
  assert.match(text(view!.root), /Không thể tải danh sách công thức/);
  fails = false;
  await flush(() => button("Thử lại").props.onClick());
  assert.match(text(view!.root), /Chưa có công thức/);
  assert.ok(view!.root.findAllByType("a").some((link) => link.props.href === "/recipes/create"));
});

test("a conflict refetches once, keeps its message, and does not retry the mutation", async () => {
  let version = "v1";
  responder = async (_path, method) => {
    if (method === "PATCH") return Response.json({ title: "Conflict" }, { status: 422 });
    return Response.json(page(1, [item("one", 1, version = "v2")], 1));
  };
  await mount();
  await flush(() => button("Xuất bản").props.onClick());
  assert.match(text(view!.root), /Dữ liệu mới nhất đã được tải lại/);
  assert.equal(requests.filter((request) => request.method === "PATCH").length, 1);
  assert.equal(requests.filter((request) => request.method === "GET").length, 2);
  assert.equal(version, "v2");
});

test("archive confirmation cancels cleanly and mutation lock blocks a duplicate request", async () => {
  let release!: () => void;
  const pending = new Promise<void>((resolve) => { release = resolve; });
  responder = async (_path, method) => {
    if (method === "PATCH") {
      await pending;
      return Response.json({ ...item("one", 3, "v2"), authorId: "author", categoryId: "category",
        description: "", instructions: "", prepTime: 0, cookTime: 0, servings: 1, difficulty: 1,
        nutrition: { calories: null, protein: null, carbs: null, fat: null } });
    }
    return Response.json(page(1, [item("one")], 1));
  };
  await mount();
  confirmResult = false;
  await flush(() => button("Lưu trữ").props.onClick());
  assert.equal(requests.filter((request) => request.method === "PATCH").length, 0);
  confirmResult = true;
  await act(async () => { button("Lưu trữ").props.onClick(); await new Promise<void>((resolve) => setImmediate(resolve)); });
  assert.ok(view!.root.findAllByType("button").filter((node) => text(node) !== "").every((node) => node.props.disabled === true));
  button("Đang cập nhật...").props.onClick();
  assert.equal(requests.filter((request) => request.method === "PATCH").length, 1);
  await flush(release);
  assert.match(text(view!.root), /Đã lưu trữ/);
});

test("Author and Admin navigation exposes the dashboard link", async () => {
  const router = { back() {}, forward() {}, refresh() {}, hmrRefresh() {}, push() {}, replace() {}, prefetch() {}, bfcacheId: "test" };
  for (const role of ["Author", "Admin"]) {
    sessionRoles.splice(0, 1, role);
    await flush(() => { view = create(<AppRouterContext.Provider value={router}><AuthControls /></AppRouterContext.Provider>); });
    await flush(() => window.dispatchEvent(new Event("culinary:session-changed")));
    assert.ok(view!.root.findAllByType("a").some((link) =>
      link.props.href === "/recipes/dashboard" && text(link) === "Công thức của tôi"));
    await act(async () => { view!.unmount(); });
    view = undefined;
  }
});

test("lifecycle success publishes its new RowVersion to the next action", async () => {
  let mutation = 0;
  responder = async (_path, method, body) => {
    if (method === "PATCH") {
      mutation++;
      assert.equal(body.rowVersion, mutation === 1 ? "v1" : "v2");
      return Response.json({ ...item("one", mutation === 1 ? 2 : 3, mutation === 1 ? "v2" : "v3"),
        authorId: "author", categoryId: "category", description: "", instructions: "", prepTime: 0,
        cookTime: 0, servings: 1, difficulty: 1, nutrition: { calories: null, protein: null, carbs: null, fat: null } });
    }
    return Response.json(page(1, [item("one")], 1));
  };
  await mount();
  await flush(() => button("Xuất bản").props.onClick());
  await flush(() => button("Lưu trữ").props.onClick());
  assert.equal(mutation, 2);
});

test("deleting the last item on a later page returns to the previous valid page", async () => {
  let currentPage = 1;
  responder = async (path, method) => {
    if (method === "DELETE") return new Response(null, { status: 204 });
    currentPage = Number(new URL(`http://localhost${path}`).searchParams.get("page"));
    if (currentPage === 2 && requests.some((request) => request.method === "DELETE"))
      return Response.json({ ...page(2, [], 1), totalCount: 1 });
    const deleted = requests.some((request) => request.method === "DELETE");
    return Response.json(currentPage === 2 ? page(2, [item("two")], 2) : page(1, [item("one")], deleted ? 1 : 2));
  };
  await mount();
  await flush(() => button("Trang sau").props.onClick());
  await flush(() => button("Xóa").props.onClick());
  assert.equal(currentPage, 1);
  assert.match(text(view!.root), /Recipe one/);
});
