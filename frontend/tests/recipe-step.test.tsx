import assert from "node:assert/strict";
import { afterEach, beforeEach, test } from "node:test";
import { useState } from "react";
import { act, create, type ReactTestInstance, type ReactTestRenderer } from "react-test-renderer";
import IngredientEditor from "../src/components/recipes/IngredientEditor";
import RecipeStepManager from "../src/components/recipes/RecipeStepManager";
import { useRecipeMutation } from "../src/components/recipes/useRecipeMutation";
import { SESSION_KEY } from "../src/services/auth-session";
import type { RecipeDetail, RecipeIngredient, RecipeStep } from "../src/types/recipe";

Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
const originalFetch = globalThis.fetch;
const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
const originalSelf = Object.getOwnPropertyDescriptor(globalThis, "self");
let view: ReactTestRenderer | undefined;
let recipe: RecipeDetail;
let calls: { path: string; method: string; body: Record<string, unknown> }[];
let version: number;
const clone = <T,>(value: T): T => JSON.parse(JSON.stringify(value));
const nextVersion = () => { recipe.rowVersion = Buffer.from(`v${++version}`).toString("base64"); };
const ingredient = (): RecipeIngredient => ({ id: "ingredient", name: "Salt", quantity: 1, unit: "g", notes: null, orderIndex: 0 });
const step = (id: string, stepNumber: number): RecipeStep => ({ id, stepNumber, title: `Title ${id}`,
  description: `Description ${id}`, timerMinutes: stepNumber * 5, imageUrl: null, rowVersion: "step-version" });

function Host() {
  const [model, setModel] = useState(recipe);
  const mutation = useRecipeMutation();
  const publishVersion = (rowVersion: string) => setModel((current) => ({ ...current, rowVersion }));
  const reload = async () => { const current = clone(recipe); setModel(current); mutation.recover(); return current; };
  return <><IngredientEditor recipeId={model.id} initialIngredients={model.ingredients} rowVersion={model.rowVersion}
    mutation={mutation} onRowVersionChange={publishVersion} onReload={reload} />
  <RecipeStepManager recipeId={model.id} initialSteps={model.steps} rowVersion={model.rowVersion}
    mutation={mutation} onRowVersionChange={publishVersion} onReload={reload} /></>;
}

beforeEach(() => {
  Object.defineProperty(globalThis, "self", { configurable: true, value: globalThis });
  const session = { sessionId: "test", accessToken: "test-access", refreshToken: "test-refresh", tokenType: "Bearer",
    expiresAt: new Date(Date.now() + 600000).toISOString(), user: { id: "owner", fullName: "Owner",
      email: "owner@example.test", userName: "owner", avatarUrl: null, roles: ["Author"] } };
  Object.defineProperty(globalThis, "window", { configurable: true, value: Object.assign(new EventTarget(), {
    confirm: () => true, localStorage: { getItem: (key: string) => key === SESSION_KEY ? JSON.stringify(session) : null },
  }) });
  version = 0;
  recipe = { id: "recipe", slug: "recipe", title: "Recipe", description: "Description", instructions: "",
    prepTime: 1, cookTime: 1, servings: 1, difficulty: 1, status: 1,
    rowVersion: Buffer.from("v0").toString("base64"), createdAt: "", publishedAt: null,
    category: { id: "category", name: "Category", slug: "category" },
    author: { id: "owner", fullName: "Owner", avatarUrl: null }, nutrition: null,
    ingredients: [ingredient()], steps: [step("A", 1), step("B", 2)], images: [] };
  calls = [];
  globalThis.fetch = async (url, options = {}) => {
    const path = new URL(String(url), "http://localhost").pathname;
    const method = options.method ?? "GET";
    const body = options.body ? JSON.parse(String(options.body)) : {};
    calls.push({ path, method, body });
    assert.equal(new Headers(options.headers).get("Authorization"), "Bearer test-access");
    if (body.rowVersion !== recipe.rowVersion) return new Response(null, { status: 422 });
    if (path.includes("/ingredients")) {
      recipe.ingredients[0] = { ...recipe.ingredients[0], name: body.name, quantity: body.quantity,
        unit: body.unit, notes: body.notes, orderIndex: body.sortOrder };
      nextVersion();
      return Response.json({ id: "ingredient", name: body.name, quantity: body.quantity, unit: body.unit,
        notes: body.notes, sortOrder: body.sortOrder, rowVersion: recipe.rowVersion });
    }
    const id = method === "POST" ? `S${recipe.steps.length + 1}` : path.split("/").at(-1)!;
    if (method === "DELETE") {
      recipe.steps = recipe.steps.filter((item) => item.id !== id)
        .map((item, index) => ({ ...item, stepNumber: index + 1 }));
      nextVersion();
      return new Response(null, { status: 204, headers: { ETag: `"${recipe.rowVersion}"` } });
    }
    const saved: RecipeStep = { id, stepNumber: method === "POST" ? recipe.steps.length + 1
      : recipe.steps.find((item) => item.id === id)!.stepNumber, title: body.title,
      description: body.description, timerMinutes: body.durationMinutes, imageUrl: body.imageUrl,
      rowVersion: "child-version" };
    recipe.steps = [...recipe.steps.filter((item) => item.id !== id), saved].sort((a, b) => a.stepNumber - b.stepNumber);
    nextVersion();
    return Response.json({ items: recipe.steps, rowVersion: recipe.rowVersion }, { status: method === "POST" ? 201 : 200 });
  };
});

afterEach(async () => {
  if (view) await act(async () => { view!.unmount(); });
  view = undefined; globalThis.fetch = originalFetch;
  if (originalWindow) Object.defineProperty(globalThis, "window", originalWindow); else Reflect.deleteProperty(globalThis, "window");
  if (originalSelf) Object.defineProperty(globalThis, "self", originalSelf); else Reflect.deleteProperty(globalThis, "self");
});

async function dispatch(action: () => void) {
  await act(async () => { action(); await new Promise<void>((resolve) => setImmediate(resolve)); });
}
async function mount() { await dispatch(() => { view = create(<Host />); }); }
function text(node: ReactTestInstance): string { return node.children.map((child) => typeof child === "string" ? child : text(child)).join(""); }
function manager() { return view!.root.findByType(RecipeStepManager); }
function stepButton(label: string, occurrence = 0) {
  return manager().findAllByType("button").filter((node) => text(node) === label)[occurrence];
}
function stepForm() { return manager().findByType("form"); }
function input(id: string) { return manager().findByProps({ id }); }
function mutations() { return calls.filter((call) => call.method !== "GET"); }

test("Step CRUD maps timerMinutes, keeps the latest RowVersion, and delete restores contiguous order", async () => {
  await mount();
  await dispatch(() => stepButton("Sửa").props.onClick());
  await dispatch(() => {
    input("recipe-step-title").props.onChange({ target: { value: "Updated title" } });
    input("recipe-step-description").props.onChange({ target: { value: "Updated description" } });
    input("recipe-step-timer").props.onChange({ target: { value: "7" } });
  });
  await dispatch(() => { void stepForm().props.onSubmit({ preventDefault() {} }); });
  await dispatch(() => {
    input("recipe-step-title").props.onChange({ target: { value: "New title" } });
    input("recipe-step-description").props.onChange({ target: { value: "New description" } });
    input("recipe-step-timer").props.onChange({ target: { value: "9" } });
  });
  await dispatch(() => { void stepForm().props.onSubmit({ preventDefault() {} }); });
  await dispatch(() => stepButton("Xóa").props.onClick());
  assert.deepEqual(mutations().map((call) => call.method), ["PUT", "POST", "DELETE"]);
  assert.deepEqual(mutations().map((call) => call.body.rowVersion), ["v0", "v1", "v2"].map((item) => Buffer.from(item).toString("base64")));
  assert.equal(mutations()[0].body.durationMinutes, 7);
  assert.deepEqual(recipe.steps.map((item) => item.stepNumber), [1, 2]);
  assert.equal(manager().props.rowVersion, recipe.rowVersion);
  assert.match(text(manager()), /9 phút/);
});

test("Step validation rejects blank description and invalid timer before transport", async () => {
  await mount();
  await dispatch(() => input("recipe-step-timer").props.onChange({ target: { value: "-1" } }));
  await dispatch(() => { void stepForm().props.onSubmit({ preventDefault() {} }); });
  assert.equal(mutations().length, 0);
  assert.match(text(manager()), /Mô tả bước nấu không được để trống/);
  await dispatch(() => input("recipe-step-description").props.onChange({ target: { value: "Valid" } }));
  await dispatch(() => { void stepForm().props.onSubmit({ preventDefault() {} }); });
  assert.equal(mutations().length, 0);
  assert.match(text(manager()), /số nguyên lớn hơn hoặc bằng 0/);
});

test("Ingredient and Step mutations share one current Recipe RowVersion in both directions", async () => {
  await mount();
  let ingredientEditor = view!.root.findByType(IngredientEditor);
  await dispatch(() => ingredientEditor.findAllByType("input")[3].props.onChange({ target: { value: "first" } }));
  await dispatch(() => ingredientEditor.findAllByType("button").find((node) => text(node) === "Lưu")!.props.onClick());
  await dispatch(() => input("recipe-step-description").props.onChange({ target: { value: "Shared version" } }));
  await dispatch(() => { void stepForm().props.onSubmit({ preventDefault() {} }); });
  ingredientEditor = view!.root.findByType(IngredientEditor);
  await dispatch(() => ingredientEditor.findAllByType("input")[3].props.onChange({ target: { value: "third" } }));
  await dispatch(() => ingredientEditor.findAllByType("button").find((node) => text(node) === "Lưu")!.props.onClick());
  assert.deepEqual(mutations().map((call) => call.body.rowVersion), ["v0", "v1", "v2"].map((item) => Buffer.from(item).toString("base64")));
  assert.deepEqual(mutations().map((call) => call.path.includes("/steps") ? "step" : "ingredient"), ["ingredient", "step", "ingredient"]);
});
