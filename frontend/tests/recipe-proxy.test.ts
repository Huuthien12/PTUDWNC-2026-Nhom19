import assert from "node:assert/strict";
import { test } from "node:test";
import { forwardRecipeRequest } from "../src/services/recipe-cache-proxy";

test("Detail always reaches backend after proxy or direct lifecycle mutations", async () => {
  for (const viaProxy of [false, true]) {
    for (const mutation of ["unpublish", "archive", "delete"]) {
      let visible = true;
      let reads = 0;
      const dependencies = {
        apiUrl: "https://backend.test", invalidate: () => {},
        fetch: async (_url: unknown, options?: RequestInit) => {
          if (options?.method !== "GET") { visible = false; return new Response(null, { status: 204 }); }
          reads++;
          assert.equal(options.cache, "no-store");
          assert.equal("next" in options, false);
          return visible ? Response.json({ status: "Published" }) : new Response(null, { status: 404 });
        },
      };
      const read = () => forwardRecipeRequest(new Request("https://frontend.test/api/recipe-cache/api/v1/recipes/pho"),
        ["api", "v1", "recipes", "pho"], dependencies);
      assert.equal((await read()).status, 200);
      if (viaProxy) await forwardRecipeRequest(new Request("https://frontend.test/api/recipe-cache", {
        method: mutation === "delete" ? "DELETE" : "PATCH", headers: { Authorization: "Bearer test" },
      }), ["api", "v1", "recipes", "id", ...(mutation === "delete" ? [] : [mutation])], dependencies);
      else visible = false;
      assert.equal((await read()).status, 404);
      assert.equal(reads, 2);
    }
  }
});

test("revalidation throw or rejection preserves committed response without resending mutation", async () => {
  for (const status of [200, 201, 204]) {
    for (const asynchronous of [false, true]) {
      let calls = 0;
      const tags: string[] = [];
      const warnings: unknown[][] = [];
      const previous = console.warn;
      console.warn = (...args) => { warnings.push(args); };
      try {
        const response = await forwardRecipeRequest(new Request("https://frontend.test/api/recipe-cache", {
          method: "POST", headers: { Authorization: "Bearer test" },
        }), ["api", "v1", "categories"], {
          apiUrl: "https://backend.test",
          invalidate: tag => {
            tags.push(tag);
            if (asynchronous) return Promise.reject(new Error("secret-provider-message"));
            throw new Error("secret-provider-message");
          },
          fetch: async () => {
            calls++;
            return new Response(status === 204 ? null : '{"id":"saved"}', {
              status, headers: { ETag: '"new"', Location: "/api/v1/categories/saved", "Content-Type": "application/json" },
            });
          },
        });
        assert.equal(response.status, status);
        assert.equal(await response.text(), status === 204 ? "" : '{"id":"saved"}');
        assert.equal(response.headers.get("ETag"), '"new"');
        assert.equal(response.headers.get("Location"), "/api/v1/categories/saved");
        assert.equal(calls, 1);
        assert.deepEqual(tags, ["recipes", "categories"]);
        assert.equal(warnings.length, 2);
        assert.equal(JSON.stringify(warnings).includes("secret-provider-message"), false);
      } finally { console.warn = previous; }
    }
  }
});

test("public proxy keeps pagination URL and tags but does not forward browser cookies", async () => {
  let observed = false;
  const response = await forwardRecipeRequest(new Request("https://frontend.test/api/recipe-cache/api/v1/recipes?page=2", {
    headers: { Cookie: "private-session=test" },
  }), ["api", "v1", "recipes"], {
    apiUrl: "https://backend.test", invalidate: () => assert.fail("GET must not invalidate"),
    fetch: async (url, options) => {
      observed = true;
      assert.equal(String(url), "https://backend.test/api/v1/recipes?page=2");
      assert.equal(new Headers(options?.headers).has("Cookie"), false);
      assert.equal(options?.cache, "force-cache");
      assert.deepEqual((options as { next: unknown }).next, { revalidate: 120, tags: ["recipes"] });
      return Response.json({ items: [] });
    },
  });
  assert.ok(observed);
  assert.equal(response.headers.get("Cache-Control"), "no-store");
  assert.deepEqual(await response.json(), { items: [] });
});

test("private GET forwards bearer with no-store and never enters shared Next cache", async () => {
  await forwardRecipeRequest(new Request("https://frontend.test/api/recipe-cache/api/v1/recipes/draft", {
    headers: { Authorization: "Bearer test-only-token" },
  }), ["api", "v1", "recipes", "draft"], {
    apiUrl: "https://backend.test", invalidate: () => assert.fail(),
    fetch: async (_url, options) => {
      assert.equal(options?.cache, "no-store");
      assert.equal("next" in options!, false);
      assert.equal(new Headers(options?.headers).get("Authorization"), "Bearer test-only-token");
      return Response.json({ status: "Draft" });
    },
  });
});

test("only a successful authorized mutation expires tags; 204 and ETag stay intact", async () => {
  for (const status of [204, 400, 401, 403, 422, 500]) {
    const tags: string[] = [];
    const response = await forwardRecipeRequest(new Request("https://frontend.test/api/recipe-cache/api/v1/recipes/id", {
      method: "DELETE", headers: { Authorization: "Bearer test-only-token", Origin: "https://frontend.test", "If-Match": '"AQ=="' },
      body: JSON.stringify({ rowVersion: "AQ==" }),
    }), ["api", "v1", "recipes", "id"], {
      apiUrl: "https://backend.test", invalidate: tag => tags.push(tag),
      fetch: async (_url, options) => {
        assert.equal(options?.cache, "no-store");
        assert.equal(options?.method, "DELETE");
        assert.equal(new Headers(options?.headers).get("If-Match"), '"AQ=="');
        assert.equal(new TextDecoder().decode(options?.body as ArrayBuffer), JSON.stringify({ rowVersion: "AQ==" }));
        return new Response(null, { status, headers: { ETag: '"AQ=="' } });
      },
    });
    assert.equal(response.status, status);
    assert.equal(response.headers.get("ETag"), '"AQ=="');
    assert.deepEqual(tags, status === 204 ? ["recipes"] : []);
  }
});

test("category and profile success expire recipe data; traversal and unauthorized origins never reach API", async () => {
  for (const path of [["api", "v1", "categories", "id"], ["api", "v1", "auth", "me"]]) {
    const tags: string[] = [];
    await forwardRecipeRequest(new Request("https://frontend.test/api/recipe-cache", {
      method: "PATCH", headers: { Authorization: "Bearer test-only-token" },
    }), path, { apiUrl: "https://backend.test", invalidate: tag => tags.push(tag), fetch: async () => Response.json({}) });
    assert.ok(tags.includes("recipes"));
    if (path[2] === "categories") assert.ok(tags.includes("categories"));
  }
  for (const [path, headers, expected] of [
    [["api", "v1", "recipes"], {}, 401],
    [["api", "v1", "recipes"], { Authorization: "Bearer test", Origin: "https://attacker.test" }, 403],
    [["..", "recipes"], { Authorization: "Bearer test" }, 400],
    [["api", "v1", "auth", "login"], { Authorization: "Bearer test" }, 404],
  ] as const) {
    const response = await forwardRecipeRequest(new Request("https://frontend.test/api/recipe-cache", { method: "POST", headers }),
      [...path], { apiUrl: "https://backend.test", invalidate: () => assert.fail(), fetch: async () => { assert.fail("must not send"); } });
    assert.equal(response.status, expected);
  }
});
