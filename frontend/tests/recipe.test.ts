import assert from "node:assert/strict";
import { test } from "node:test";
import { buildRecipePayload, initialRecipeValues, mapRecipeError, recipeCreatedDestination, recipeRoles } from "../src/services/recipe-form";
import { ApiError, httpClient } from "../src/services/http-client";
import { changeRecipeLifecycle, createRecipe } from "../src/services/recipe-service";
import { dashboardActions, lifecycleErrorMessage } from "../src/services/recipe-dashboard";
import { canAccess, loginDestination } from "../src/services/auth-navigation";
import { SESSION_KEY } from "../src/services/auth-session";

const categoryId = "11111111-1111-1111-1111-111111111111";
const valid = { ...initialRecipeValues, title: "Canh chua", categoryId, prepTime: "10", cookTime: "20", servings: "2" };

test("create payload uses contract names and excludes server-owned fields and empty optional values", () => {
  const result = buildRecipePayload({ ...valid, title: "  Canh chua  " }, [categoryId]);
  assert.deepEqual(result.payload, {
    title: "  Canh chua  ", description: "", categoryId, prepTime: 10, cookTime: 20, servings: 2, difficulty: 1,
  });
});

test("partial nutrition preserves zero, decimals, nulls and optional instructions", () => {
  const result = buildRecipePayload({ ...valid, instructions: "Nấu chín", calories: "0", protein: "12.5", carbs: "  " }, [categoryId]);
  assert.deepEqual(result.payload?.nutrition, { calories: 0, protein: 12.5, carbs: null, fat: null });
  assert.equal(result.payload?.instructions, "Nấu chín");
});

test("all blank nutrition is omitted while four explicit zeros are retained", () => {
  assert.equal(buildRecipePayload({ ...valid, calories: " ", protein: "", carbs: " ", fat: "" }, [categoryId]).payload?.nutrition, undefined);
  assert.deepEqual(buildRecipePayload({ ...valid, calories: "0", protein: "0", carbs: "0", fat: "0" }, [categoryId]).payload?.nutrition,
    { calories: 0, protein: 0, carbs: 0, fat: 0 });
});

test("validation rejects missing/stale categories, title bounds and whitespace", () => {
  for (const title of ["abcd", " ".repeat(6), "a".repeat(201)]) {
    assert.ok(buildRecipePayload({ ...valid, title }, [categoryId]).errors?.title);
  }
  for (const title of ["a".repeat(5), "a".repeat(200)]) {
    assert.ok(buildRecipePayload({ ...valid, title }, [categoryId]).payload);
  }
  assert.ok(buildRecipePayload(valid, []).errors?.categoryId);
  assert.ok(buildRecipePayload({ ...valid, categoryId: "" }, [categoryId]).errors?.categoryId);
});

test("create requires positive integer times/servings and the numeric difficulty enum", () => {
  for (const field of ["prepTime", "cookTime", "servings"] as const) {
    for (const value of ["", "0", "-1", "1.5", "Infinity", "2147483648"]) {
      assert.ok(buildRecipePayload({ ...valid, [field]: value }, [categoryId]).errors?.[field]);
    }
  }
  for (const difficulty of ["0", "4", "Easy", ""]) {
    assert.ok(buildRecipePayload({ ...valid, difficulty }, [categoryId]).errors?.difficulty);
  }
});

test("optional nutrition rejects negative/non-finite numbers without inventing a precision cap", () => {
  for (const field of ["calories", "protein", "carbs", "fat"] as const) {
    for (const value of ["-1", "NaN", "Infinity", "abc"]) {
      assert.ok(buildRecipePayload({ ...valid, [field]: value }, [categoryId]).errors?.[field]);
    }
    assert.equal(buildRecipePayload({ ...valid, [field]: "0.123456" }, [categoryId]).payload?.nutrition?.[field], 0.123456);
  }
});

test("field mapping accepts PascalCase, nested nutrition, binding paths and ignores unknown fields", () => {
  const mapped = mapRecipeError(new ApiError("secret", 400,
    ["Title", "CategoryId", "Nutrition.Protein", "$.cookTime", "request.Nutrition.Fat", "__proto__", "unknown"]));
  assert.deepEqual(Object.keys(mapped.fields).sort(), ["title", "categoryId", "protein", "cookTime", "fat"].sort());
  assert.ok(!JSON.stringify(mapped).includes("secret"));
});

test("401/403/409 and unexpected errors give safe actionable messages", () => {
  assert.match(mapRecipeError(new ApiError("secret", 401)).message, /đăng nhập/);
  assert.match(mapRecipeError(new ApiError("secret", 403)).message, /quyền/);
  assert.ok(mapRecipeError(new ApiError("secret", 409)).fields.title);
  for (const error of [new ApiError("secret", 400), new ApiError("secret", 500), new TypeError("secret")]) {
    assert.ok(mapRecipeError(error).message);
    assert.ok(!JSON.stringify(mapRecipeError(error)).includes("secret"));
  }
});

test("UI access and login return path allow Author/Admin only, destination needs no detail API", () => {
  for (const role of ["Author", "Admin"]) assert.ok(canAccess([role], recipeRoles));
  for (const roles of [[], ["Reader"], ["author"]]) assert.equal(canAccess(roles, recipeRoles), false);
  assert.equal(loginDestination("/recipes/create"), "/recipes/create");
  assert.equal(loginDestination("/recipes/create?next=https://evil.test"), "/");
  assert.equal(recipeCreatedDestination, "/categories?created=recipe");
});

test("transport keeps only validation field paths and tolerates malformed/empty errors", async () => {
  const original = globalThis.fetch;
  try {
    for (const body of [JSON.stringify({ errors: { Title: ["secret-token"], "Nutrition.Carbs": ["private"] }, detail: "secret-token" }), "not json", "", "null"]) {
      globalThis.fetch = async () => new Response(body, { status: 400 });
      await assert.rejects(httpClient("/api/v1/recipes"), (error: unknown) => {
        assert.ok(error instanceof ApiError);
        assert.equal(error.status, 400);
        assert.deepEqual(error.validationFields, body.startsWith("{") ? ["Title", "Nutrition.Carbs"] : []);
        assert.ok(!JSON.stringify(error).includes("secret-token"));
        return true;
      });
    }
  } finally { globalThis.fetch = original; }
});

test("recipe service POST sends authenticated JSON and returns direct 201 DTO", async () => {
  const originalFetch = globalThis.fetch;
  const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
  const session = {
    sessionId: "recipe-test", accessToken: "test-access", refreshToken: "test-refresh", tokenType: "Bearer",
    expiresAt: new Date(Date.now() + 600000).toISOString(),
    user: { id: "author", fullName: "Author", userName: "author", email: "a@example.test", avatarUrl: null, roles: ["Author"] },
  };
  Object.defineProperty(globalThis, "window", { configurable: true, value: {
    localStorage: { getItem: (key: string) => key === SESSION_KEY ? JSON.stringify(session) : null },
  } });
  const payload = buildRecipePayload(valid, [categoryId]).payload!;
  const dto = { ...payload, id: "created", slug: "canh-chua", status: 1 };
  let calls = 0;
  globalThis.fetch = async (url, options = {}) => {
    calls++;
    assert.ok(String(url).endsWith("/api/v1/recipes"));
    assert.equal(options.method, "POST");
    assert.equal(new Headers(options.headers).get("Authorization"), "Bearer test-access");
    assert.equal(new Headers(options.headers).get("Content-Type"), "application/json");
    assert.deepEqual(JSON.parse(String(options.body)), payload);
    return Response.json(dto, { status: 201 });
  };
  try {
    assert.deepEqual(await createRecipe(payload), dto);
    assert.equal(calls, 1);

    // Business errors must reach the form without replaying the POST or refreshing a token.
    for (const status of [400, 403, 409]) {
      calls = 0;
      globalThis.fetch = async () => {
        calls++;
        return Response.json({ type: status === 409 ? "RECIPE_SLUG_EXISTS" : "VALIDATION_ERROR",
          errors: { "Request.Nutrition.Protein": ["invalid"] } }, { status });
      };
      await assert.rejects(createRecipe(payload), (error: unknown) => {
        assert.ok(error instanceof ApiError);
        assert.equal(error.status, status);
        const mapped = mapRecipeError(error);
        if (status === 400) assert.ok(mapped.fields.protein);
        if (status === 409) assert.ok(mapped.fields.title);
        return true;
      });
      assert.equal(calls, 1);
    }
  } finally {
    globalThis.fetch = originalFetch;
    if (originalWindow) Object.defineProperty(globalThis, "window", originalWindow);
    else Reflect.deleteProperty(globalThis, "window");
  }
});

test("dashboard actions follow lifecycle states without offering unarchive or delete", () => {
  assert.deepEqual(dashboardActions(1), ["publish", "archive"]);
  assert.deepEqual(dashboardActions(2), ["unpublish", "archive"]);
  assert.deepEqual(dashboardActions(3), []);
});

test("dashboard lifecycle transport sends the current RowVersion to the correct route", async () => {
  const originalFetch = globalThis.fetch;
  const originalWindow = Object.getOwnPropertyDescriptor(globalThis, "window");
  const session = { sessionId: "dashboard", accessToken: "access", refreshToken: "refresh", tokenType: "Bearer", expiresAt: new Date(Date.now() + 600000).toISOString(), user: { id: "author", fullName: "Author", userName: "author", email: "a@example.test", avatarUrl: null, roles: ["Author"] } };
  Object.defineProperty(globalThis, "window", { configurable: true, value: { localStorage: { getItem: (key: string) => key === SESSION_KEY ? JSON.stringify(session) : null } } });
  try {
    for (const action of ["publish", "unpublish", "archive"] as const) {
      globalThis.fetch = async (url, options = {}) => {
        assert.ok(String(url).endsWith(`/api/v1/recipes/recipe-id/${action}`));
        assert.equal(options.method, "PATCH");
        assert.equal(new Headers(options.headers).get("Authorization"), "Bearer access");
        assert.deepEqual(JSON.parse(String(options.body)), { rowVersion: "current-token" });
        return Response.json({ id: "recipe-id", rowVersion: "next-token", status: 2 });
      };
      const result = await changeRecipeLifecycle("recipe-id", action, "current-token");
      assert.equal(result.rowVersion, "next-token");
    }
    assert.match(lifecycleErrorMessage(new ApiError("hidden", 422)), /tải lại/);
    assert.match(lifecycleErrorMessage(new ApiError("hidden", 400)), /chưa đủ điều kiện/);
  } finally {
    globalThis.fetch = originalFetch;
    if (originalWindow) Object.defineProperty(globalThis, "window", originalWindow);
    else Reflect.deleteProperty(globalThis, "window");
  }
});
