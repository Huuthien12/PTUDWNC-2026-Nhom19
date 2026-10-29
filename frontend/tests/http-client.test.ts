import assert from "node:assert/strict";
import { afterEach, test } from "node:test";
import { ApiError, httpClient, parseRetryAfter } from "../src/services/http-client";

const originalFetch = globalThis.fetch;
afterEach(() => { globalThis.fetch = originalFetch; });

test("SSR uses the internal API URL", async () => {
  const previous = process.env.API_URL;
  process.env.API_URL = "http://api:8080";
  let target = "";
  globalThis.fetch = async (input) => {
    target = String(input);
    return new Response(JSON.stringify({ ok: true }));
  };

  try {
    await httpClient("/api/v1/auth/login", { method: "POST" });
    assert.equal(target, "http://api:8080/api/v1/auth/login");
  } finally {
    if (previous === undefined) delete process.env.API_URL;
    else process.env.API_URL = previous;
  }
});

test("parses RFC 7807 validation problems without serializing server details", async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({
    type: "VALIDATION_ERROR", title: "Validation failed.", status: 400,
    detail: "private validation detail", instance: "/api/v1/recipes", traceId: "trace-1",
    errors: { Title: ["private message"], "Nutrition.Protein": ["invalid"] }, errorCode: "VALIDATION_ERROR",
  }), { status: 400, headers: { "Content-Type": "application/problem+json" } });

  await assert.rejects(httpClient("/api/v1/recipes"), (error: unknown) => {
    assert.ok(error instanceof ApiError);
    assert.equal(error.status, 400);
    assert.deepEqual(error.validationFields, ["Title", "Nutrition.Protein"]);
    assert.equal(error.problem?.traceId, "trace-1");
    assert.equal(error.problem?.extensions?.errorCode, "VALIDATION_ERROR");
    assert.ok(!JSON.stringify(error).includes("private validation detail"));
    return true;
  });
});

test("keeps all required HTTP status codes distinguishable", async () => {
  for (const status of [400, 401, 403, 404, 409, 422, 423, 429]) {
    globalThis.fetch = async () => new Response(JSON.stringify({ title: "Problem", status }), {
      status,
      headers: status === 429 ? { "Retry-After": "12" } : undefined,
    });
    await assert.rejects(httpClient("/api/v1/test"), (error: unknown) => {
      assert.ok(error instanceof ApiError);
      assert.equal(error.status, status);
      assert.equal(error.kind, "http");
      assert.equal(error.retryAfter, status === 429 ? 12 : undefined);
      return true;
    });
  }
});

test("uses the HTTP status when a ProblemDetails body reports a different status", async () => {
  globalThis.fetch = async () => new Response(JSON.stringify({ title: "Too many requests", status: 200 }), {
    status: 429,
    headers: { "Retry-After": "12" },
  });

  await assert.rejects(httpClient("/api/v1/test"), (error: unknown) => {
    assert.ok(error instanceof ApiError);
    assert.equal(error.status, 429);
    assert.equal(error.problem?.status, 429);
    assert.equal(error.retryAfter, 12);
    return true;
  });
});

test("parses Retry-After delta seconds and HTTP dates", () => {
  assert.equal(parseRetryAfter("5", 0), 5);
  assert.equal(parseRetryAfter("Wed, 21 Oct 2015 07:28:00 GMT", Date.parse("Wed, 21 Oct 2015 07:27:55 GMT")), 5);
  assert.equal(parseRetryAfter("invalid"), undefined);
});

test("tolerates malformed, non-JSON and empty error responses", async () => {
  for (const body of ["not-json", "", "null"]) {
    globalThis.fetch = async () => new Response(body, { status: 500, headers: { "Content-Type": "text/plain" } });
    await assert.rejects(httpClient("/api/v1/test"), (error: unknown) => {
      assert.ok(error instanceof ApiError);
      assert.equal(error.status, 500);
      assert.equal(error.problem, undefined);
      assert.deepEqual(error.validationFields, []);
      return true;
    });
  }
});

test("normalizes network, timeout and abort failures without exposing provider messages", async () => {
  const failures: [Error, ApiError["kind"]][] = [
    [new TypeError("network provider detail"), "network"],
    [new DOMException("timeout provider detail", "TimeoutError"), "timeout"],
    [new DOMException("abort provider detail", "AbortError"), "aborted"],
  ];
  for (const [failure, kind] of failures) {
    globalThis.fetch = async () => { throw failure; };
    await assert.rejects(httpClient("/api/v1/test"), (error: unknown) => {
      assert.ok(error instanceof ApiError);
      assert.equal(error.status, 0);
      assert.equal(error.kind, kind);
      assert.ok(!error.message.includes("provider detail"));
      return true;
    });
  }
});
