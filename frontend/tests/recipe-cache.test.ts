import assert from "node:assert/strict";
import { test } from "node:test";
import { publicRecipeCache, recipeProxyPath } from "../src/services/recipe-cache-policy";
import { clearRecipeRequests, publicRecipeRequest } from "../src/services/recipe-requests";

test("Next public cache lifetimes and tags exclude private reads and all mutations", () => {
  assert.deepEqual(publicRecipeCache("/api/v1/recipes", "GET", false), { revalidate: 120, tags: ["recipes"] });
  assert.equal(publicRecipeCache("/api/v1/recipes/pho-bo", "GET", false), null);
  for (const path of ["/api/v1/recipes", "/api/v1/recipes/pho-bo"])
    for (const method of ["GET", "POST", "PUT", "PATCH", "DELETE"])
      assert.equal(publicRecipeCache(path, method, true), null);
  assert.equal(publicRecipeCache("/api/v1/recipes/pho-bo", "PUT", false), null);
  assert.equal(publicRecipeCache("/api/v1/recipes/search", "GET", false), null);
  assert.equal(publicRecipeCache("/api/v1/recipes/suggestions", "GET", false), null);
});

test("proxy scope preserves auth endpoints and only routes existing related mutations", () => {
  assert.equal(recipeProxyPath("/api/v1/recipes?page=2"), true);
  assert.equal(recipeProxyPath("/api/v1/recipes/id/images/id/primary", "PATCH"), true);
  assert.equal(recipeProxyPath("/api/v1/categories/id", "PUT"), true);
  assert.equal(recipeProxyPath("/api/v1/categories"), false);
  assert.equal(recipeProxyPath("/api/v1/auth/me", "PATCH"), true);
  assert.equal(recipeProxyPath("/api/v1/auth/me"), false);
  assert.equal(recipeProxyPath("/api/v1/auth/login", "POST"), false);
  assert.equal(recipeProxyPath("https://attacker.test/api/v1/recipes"), false);
});

test("concurrent identical public reads coalesce; pages differ and completed results are not retained", async () => {
  clearRecipeRequests();
  let resolve!: (value: number) => void;
  let calls = 0;
  const read = () => { calls++; return new Promise<number>(done => { resolve = done; }); };
  const first = publicRecipeRequest("page=1", read);
  assert.equal(first, publicRecipeRequest("page=1", read));
  assert.equal(calls, 1);
  assert.equal(await publicRecipeRequest("page=2", async () => 2), 2);
  resolve(1);
  assert.equal(await first, 1);
  assert.equal(await publicRecipeRequest("page=1", async () => 3), 3);
});

test("mutation clears pending reads and failure allows retry without retaining rejected promises", async () => {
  clearRecipeRequests();
  const stale = publicRecipeRequest("detail", () => new Promise<number>(() => {}));
  clearRecipeRequests();
  const fresh = publicRecipeRequest("detail", async () => 4);
  assert.notEqual(stale, fresh);
  assert.equal(await fresh, 4);
  await assert.rejects(publicRecipeRequest("failed", async () => { throw new Error("test"); }));
  assert.equal(await publicRecipeRequest("failed", async () => 5), 5);
});
