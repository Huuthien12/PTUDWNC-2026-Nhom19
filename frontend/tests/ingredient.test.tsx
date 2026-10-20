import assert from "node:assert/strict";
import { afterEach, beforeEach, test } from "node:test";
import { act, create, type ReactTestRenderer, type ReactTestInstance } from "react-test-renderer";
import { Suspense } from "react";
import RecipeDetailPage from "../src/app/recipes/[slug]/page";
import EditRecipeForm from "../src/components/recipes/EditRecipeForm";
import IngredientEditor from "../src/components/recipes/IngredientEditor";
import { SESSION_KEY } from "../src/services/auth-session";
import { ingredientError, validateIngredient } from "../src/services/ingredient-form";
import { ApiError } from "../src/services/api-client";
import type { RecipeDetail, RecipeIngredient, RecipeDto } from "../src/types/recipe";

Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
const originalFetch = globalThis.fetch;
const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
const originalSelf = Object.getOwnPropertyDescriptor(globalThis, "self");
let view: ReactTestRenderer | undefined;
let recipe: RecipeDetail;
let version: number;
let calls: { path: string; method: string; body: Record<string, unknown> }[];
let intercept: ((path: string, method: string, body: Record<string, unknown>) => Promise<Response | undefined>) | undefined;
const ingredient = (id: string, orderIndex: number): RecipeIngredient =>
  ({ id, orderIndex, name: id, quantity: 1, unit: "g", notes: null });
const clone = <T,>(value: T): T => JSON.parse(JSON.stringify(value));
const nextVersion = () => { recipe.rowVersion = Buffer.from(`v${++version}`).toString("base64"); };

function browser(userId = "owner", roles = ["Author"]) {
  const session = { sessionId: `test-${userId}`, accessToken: "test-only-access", refreshToken: "test-only-refresh",
    tokenType: "Bearer", expiresAt: new Date(Date.now() + 600000).toISOString(),
    user: { id: userId, fullName: "Test", email: "test@example.test", userName: "test", avatarUrl: null, roles } };
  Object.defineProperty(globalThis, "window", { configurable: true, value: Object.assign(new EventTarget(), {
    confirm: () => true, localStorage: { getItem: (key: string) => key === SESSION_KEY ? JSON.stringify(session) : null },
  }) });
}

beforeEach(() => {
  Object.defineProperty(globalThis, "self", { configurable: true, value: globalThis });
  browser();
  version = 0;
  recipe = { id: "recipe", slug: "recipe", title: "Recipe title", description: "original",
    instructions: "", prepTime: 10, cookTime: 20, servings: 2, difficulty: 1, status: 2,
    rowVersion: Buffer.from("v0").toString("base64"), createdAt: "", publishedAt: null,
    author: { id: "owner", fullName: "Owner", avatarUrl: null },
    category: { id: "category", name: "Category", slug: "category" },
    nutrition: null, ingredients: [ingredient("A", 0), ingredient("B", 1)], steps: [], images: [] };
  calls = [];
  intercept = undefined;
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), "http://localhost").pathname;
    const method = options.method ?? "GET";
    const body = options.body ? JSON.parse(String(options.body)) : {};
    calls.push({ path, method, body });
    const intercepted = await intercept?.(path, method, body);
    if (intercepted) return intercepted;
    if (path === "/api/v1/categories") return Response.json([recipe.category]);
    if (method === "GET") return Response.json(clone(recipe));
    assert.equal(new Headers(options.headers).get("Authorization"), "Bearer test-only-access");
    if (body.rowVersion !== recipe.rowVersion) return new Response(null, { status: 422 });
    if (path.includes("/ingredients")) {
      const id = method === "POST" ? "C" : path.split("/").at(-1)!;
      if (method === "DELETE") {
        recipe.ingredients = recipe.ingredients.filter((item) => item.id !== id);
        nextVersion();
        return new Response(null, { status: 204, headers: { ETag: `"${recipe.rowVersion}"` } });
      }
      const saved: RecipeIngredient = { id, name: body.name, quantity: body.quantity, unit: body.unit,
        notes: body.notes, orderIndex: body.sortOrder };
      recipe.ingredients = [...recipe.ingredients.filter((item) => item.id !== id), saved];
      nextVersion();
      const { orderIndex, ...content } = saved;
      return Response.json({ ...content, sortOrder: orderIndex, rowVersion: recipe.rowVersion },
        { status: method === "POST" ? 201 : 200 });
    }
    const { rowVersion: _version, ...input } = body;
    void _version;
    Object.assign(recipe, input);
    nextVersion();
    const dto: RecipeDto = { ...input, id: recipe.id, slug: recipe.slug, authorId: recipe.author.id,
      status: recipe.status, rowVersion: recipe.rowVersion, createdAt: "", updatedAt: "", publishedAt: null };
    return Response.json(dto);
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

async function dispatch(action: () => void) {
  await act(async () => { action(); await new Promise<void>((resolve) => setImmediate(resolve)); });
}
async function mount() { await dispatch(() => { view = create(<EditRecipeForm slug="recipe" />); }); }
function text(node: ReactTestInstance): string { return node.children.map((child) => typeof child === "string" ? child : text(child)).join(""); }
function button(label: string, occurrence = 0) {
  return view!.root.findAllByType("button").filter((node) => text(node) === label)[occurrence];
}
function editor() { return view!.root.findByType(IngredientEditor); }
function rootForm() { return view!.root.findAllByType("form")[0]; }
function rows() { return editor().findAll((node) => node.type === "div" && typeof node.props.draggable === "boolean"); }
function input(id: string, field: number) {
  return rows().find((row) => row.findAllByType("input")[0].props.value === id)!.findAllByType("input")[field];
}
async function change(id: string, field: number, value: string) {
  await dispatch(() => input(id, field).props.onChange({ target: { value } }));
}
function mutations() { return calls.filter((call) => call.method !== "GET"); }
async function click(label: string, occurrence = 0) { await dispatch(() => button(label, occurrence).props.onClick()); }
async function submit() { await dispatch(() => { void rootForm().props.onSubmit({ preventDefault() {} }); }); }

test("saving A retains B's dirty fields and a new draft, without a RowVersion remount", async () => {
  await mount();
  await change("B", 3, "unsaved B");
  await click("Thêm nguyên liệu");
  await change("A", 3, "saved A");
  await click("Lưu", 0);
  assert.equal(recipe.ingredients.find((item) => item.id === "A")!.notes, "saved A");
  assert.equal(input("B", 3).props.value, "unsaved B");
  assert.equal(rows().length, 3);
  assert.match(text(editor()), /Đã lưu nguyên liệu/);
  assert.equal(editor().props.rowVersion, recipe.rowVersion);
});

test("add/update/delete retain the exact latest version and use mutation sortOrder + DELETE ETag", async () => {
  await mount();
  await change("A", 3, "saved");
  await click("Lưu", 0);
  await click("Thêm nguyên liệu");
  const last = rows().at(-1)!;
  await dispatch(() => last.findAllByType("input")[0].props.onChange({ target: { value: "C" } }));
  await change("C", 2, "ml");
  await click("Lưu", 2);
  await click("Xóa", 0);
  assert.deepEqual(mutations().map((call) => call.method), ["PUT", "POST", "DELETE"]);
  assert.deepEqual(mutations().map((call) => call.body.rowVersion),
    ["v0", "v1", "v2"].map((value) => Buffer.from(value).toString("base64")));
  assert.equal(mutations()[1].body.quantity, null);
  assert.ok("sortOrder" in mutations()[0].body);
  assert.ok(!("orderIndex" in mutations()[0].body));
  assert.equal(editor().props.rowVersion, recipe.rowVersion);
  assert.equal(rows().length, 2);
  assert.equal(rows()[0].findAllByType("input")[0].props.value, "B");
});

test("partial reorder reloads actual server order and publishes each successful version", async () => {
  await mount();
  let count = 0;
  intercept = async (_path, method) => method === "PUT" && ++count === 2 ? new Response(null, { status: 422 }) : undefined;
  await click("Lên", 1);
  await click("Lưu thứ tự");
  assert.deepEqual(mutations().map((call) => call.body.rowVersion),
    ["v0", "v1"].map((value) => Buffer.from(value).toString("base64")));
  assert.equal(editor().props.rowVersion, recipe.rowVersion);
  const displayed = rows().map((row) => row.findAllByType("input")[0].props.value);
  assert.deepEqual(displayed, [...recipe.ingredients].sort((a, b) => a.orderIndex - b.orderIndex).map((item) => item.name));
  assert.match(text(editor()), /cập nhật một phần/);
  assert.equal(button("Lưu").props.disabled, false);
});

test("reorder persists only committed content, preserving even invalid unsaved drafts", async () => {
  await mount();
  await change("B", 3, "draft note");
  await change("B", 1, "0");
  await click("Lên", 1);
  await click("Lưu thứ tự");
  assert.equal(mutations().length, 2);
  for (const call of mutations()) { assert.equal(call.body.notes, null); assert.equal(call.body.quantity, 1); }
  assert.equal(input("B", 3).props.value, "draft note");
  assert.equal(input("B", 1).props.value, 0);
  assert.deepEqual(recipe.ingredients.sort((a, b) => a.orderIndex - b.orderIndex).map((item) => item.id), ["B", "A"]);
});

test("saving content also preserves a pending ordering draft without persisting it implicitly", async () => {
  await mount();
  await click("Lên", 1);
  await click("Lưu", 1);
  assert.equal(mutations()[0].body.sortOrder, 0);
  assert.deepEqual(rows().map((row) => row.findAllByType("input")[0].props.value), ["B", "A"]);
  await click("Lưu thứ tự");
  assert.deepEqual([...recipe.ingredients].sort((a, b) => a.orderIndex - b.orderIndex).map((item) => item.id), ["B", "A"]);
});

test("shared parent/child lock prevents both overlap directions and same-tick double submit", async () => {
  await mount();
  let release!: () => void;
  const gate = new Promise<void>((resolve) => { release = resolve; });
  intercept = async (_path, method) => { if (method !== "GET") await gate; return undefined; };
  const save = button("Lưu").props.onClick;
  const rootSubmit = rootForm().props.onSubmit;
  await dispatch(() => { save(); save(); void rootSubmit({ preventDefault() {} }); });
  assert.equal(mutations().length, 1);
  assert.equal(view!.root.findAllByType("form")[0].findAllByProps({ type: "submit" })[0].props.disabled, true);
  assert.ok(editor().findAllByType("input").every((node) => node.props.disabled));
  await dispatch(() => release());
  intercept = undefined;
  let releaseRoot!: () => void;
  const rootGate = new Promise<void>((resolve) => { releaseRoot = resolve; });
  intercept = async (path, method) => { if (method === "PUT" && !path.includes("/ingredients")) await rootGate; return undefined; };
  const childSave = button("Lưu").props.onClick;
  const currentSubmit = rootForm().props.onSubmit;
  await dispatch(() => { void currentSubmit({ preventDefault() {} }); childSave(); void currentSubmit({ preventDefault() {} }); });
  assert.equal(mutations().length, 2);
  await dispatch(() => releaseRoot());
});

test("component rejects name/unit/notes/quantity violations before transport", async () => {
  await mount();
  const inputs = rows()[0].findAllByType("input");
  await dispatch(() => {
    inputs[0].props.onChange({ target: { value: "x".repeat(101) } });
    inputs[1].props.onChange({ target: { value: "0" } });
    inputs[2].props.onChange({ target: { value: "x".repeat(51) } });
    inputs[3].props.onChange({ target: { value: "x".repeat(501) } });
  });
  await click("Lưu");
  assert.equal(mutations().length, 0);
  assert.ok(rows()[0].findAllByType("input").every((node) => node.props["aria-invalid"]));
  assert.deepEqual(validateIngredient({ ...ingredient("x".repeat(100), 0), unit: "x".repeat(50), notes: "x".repeat(500), quantity: null }), {});
  for (const quantity of [0, -1, NaN, Infinity]) assert.ok(validateIngredient({ ...ingredient("A", 0), quantity }).quantity);
  assert.ok(validateIngredient({ ...ingredient(" ", 0), unit: " " }).name);
  assert.ok(validateIngredient({ ...ingredient("A", 0), unit: " " }).unit);
});

test("append uses max orderIndex + 1 for non-contiguous server ordering", async () => {
  recipe.ingredients = [ingredient("A", 10), ingredient("B", 20)];
  await mount();
  await click("Thêm nguyên liệu");
  await dispatch(() => rows()[2].findAllByType("input")[0].props.onChange({ target: { value: "C" } }));
  await change("C", 2, "g");
  await click("Lưu", 2);
  assert.equal(mutations()[0].body.sortOrder, 21);
  assert.deepEqual(rows().map((row) => row.findAllByType("input")[0].props.value), ["A", "B", "C"]);
});

for (const [userId, roles, allowed] of [
  ["other", ["Author"], false], ["owner", ["Author"], true], ["admin", ["Admin"], true], ["owner", ["Reader"], false],
] as const) test(`edit page ownership gating: ${userId}/${roles[0]}`, async () => {
  browser(userId, [...roles]);
  await mount();
  assert.equal(view!.root.findAllByType(IngredientEditor).length, allowed ? 1 : 0);
  assert.equal(button("Lưu thay đổi").props.disabled, !allowed);
});

for (const [userId, roles, allowed] of [
  ["other", ["Author"], false], ["owner", ["Author"], true], ["admin", ["Admin"], true],
] as const) test(`detail page ownership gating: ${userId}/${roles[0]}`, async () => {
  browser(userId, [...roles]);
  const params = Promise.resolve({ slug: "recipe" });
  await dispatch(() => { view = create(<Suspense fallback={<p>loading</p>}><RecipeDetailPage params={params} /></Suspense>); });
  assert.equal(view!.root.findAllByType(IngredientEditor).length, allowed ? 1 : 0);
});

test("drag-and-drop order is persisted with the same semantics as the arrow buttons", async () => {
  await mount();
  await dispatch(() => rows()[1].props.onDragStart());
  await dispatch(() => rows()[0].props.onDrop());
  await click("Lưu thứ tự");
  assert.deepEqual([...recipe.ingredients].sort((a, b) => a.orderIndex - b.orderIndex).map((item) => item.id), ["B", "A"]);
});

for (const withEtag of [true, false]) test(`DELETE + failed reload blocks subsequent mutations (ETag=${withEtag})`, async () => {
  await mount();
  let deleted = false;
  intercept = async (path, method) => {
    if (method === "DELETE") {
      recipe.ingredients = recipe.ingredients.filter((item) => item.id !== "A");
      nextVersion();
      deleted = true;
      return new Response(null, { status: 204, headers: withEtag ? { ETag: `"${recipe.rowVersion}"` } : {} });
    }
    if (deleted && method === "GET" && path.includes("/recipes")) throw new TypeError("test offline");
    return undefined;
  };
  const staleSave = button("Lưu", 1).props.onClick;
  await click("Xóa");
  assert.equal(editor().props.rowVersion, withEtag ? recipe.rowVersion : Buffer.from("v0").toString("base64"));
  assert.equal(rows().length, 1);
  await dispatch(() => { staleSave(); void rootForm().props.onSubmit({ preventDefault() {} }); });
  assert.equal(mutations().length, 1);
  assert.equal(button("Lưu").props.disabled, true);
  intercept = undefined;
  await click("Tải lại dữ liệu");
  assert.equal(editor().props.rowVersion, recipe.rowVersion);
  assert.equal(button("Lưu").props.disabled, false);
});

test("new server core snapshot blocks a stale parent PUT, keeping its unsaved values until explicit reload", async () => {
  await mount();
  await dispatch(() => view!.root.findByProps({ id: "title" }).props.onChange({ target: { value: "My draft title" } }));
  let changed = false;
  intercept = async (path, method) => {
    if (path.includes("/recipes") && method === "GET" && version > 0 && !changed) {
      changed = true; recipe.description = "other session description"; nextVersion();
    }
    return undefined;
  };
  await click("Lưu");
  assert.equal(view!.root.findByProps({ id: "title" }).props.value, "My draft title");
  assert.equal(view!.root.findByProps({ id: "description" }).props.value, "original");
  assert.equal(button("Lưu thay đổi").props.disabled, true);
  await submit();
  assert.equal(mutations().filter((call) => !call.path.includes("/ingredients")).length, 0);
  assert.equal(recipe.description, "other session description");
  await click("Tải dữ liệu mới (bỏ bản nháp công thức)");
  assert.equal(view!.root.findByProps({ id: "description" }).props.value, "other session description");
  assert.equal(button("Lưu thay đổi").props.disabled, false);
});

test("422 preserves conflicting ingredient draft but requires an explicit reapply choice", async () => {
  await mount();
  await change("A", 3, "my draft");
  recipe.ingredients[0].notes = "other session"; nextVersion();
  await click("Lưu");
  assert.equal(input("A", 3).props.value, "my draft");
  assert.equal(button("Lưu").props.disabled, true);
  assert.match(text(editor()), /Nội dung đã thay đổi/);
  assert.equal(recipe.ingredients[0].notes, "other session");
  await click("Giữ bản nháp");
  await click("Lưu");
  assert.equal(recipe.ingredients.find((item) => item.id === "A")!.notes, "my draft");
});

test("400/401/403/404/422 errors have distinct safe user messages", () => {
  const messages = [400, 401, 403, 404, 422].map((status) => ingredientError(new ApiError("secret must not surface", status)));
  assert.equal(new Set(messages).size, 5);
  assert.ok(messages.every((message) => !message.includes("secret")));
});
