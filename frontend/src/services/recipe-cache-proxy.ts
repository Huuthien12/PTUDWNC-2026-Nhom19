import { publicRecipeCache, recipeProxyPath } from "./recipe-cache-policy";

export async function forwardRecipeRequest(request: Request, segments: string[], dependencies: {
  apiUrl: string; fetch: typeof fetch; invalidate: (tag: string) => void;
}) {
  if (segments.some(segment => !/^[a-zA-Z0-9_-]+$/.test(segment)))
    return new Response(null, { status: 400 });
  const path = `/${segments.join("/")}`;
  const method = request.method;
  if (!recipeProxyPath(path, method)) return new Response(null, { status: 404 });
  const authorization = request.headers.get("Authorization");
  if (method !== "GET") {
    // Invalidation requires a successful mutation authorized by the backend.
    if (!authorization) return new Response(null, { status: 401 });
    const origin = request.headers.get("Origin");
    if (origin && origin !== new URL(request.url).origin) return new Response(null, { status: 403 });
  }
  const headers = new Headers();
  if (authorization) headers.set("Authorization", authorization);
  for (const name of ["Content-Type", "If-Match", "Accept"])
    if (request.headers.has(name)) headers.set(name, request.headers.get(name)!);
  const policy = publicRecipeCache(path, method, authorization !== null);
  const upstream = await dependencies.fetch(`${dependencies.apiUrl}${path}${new URL(request.url).search}`, {
    method, headers, redirect: "error",
    body: method === "GET" ? undefined : await request.arrayBuffer(),
    ...(policy ? { cache: "force-cache", next: policy } : { cache: "no-store" }),
  });
  if (method !== "GET" && upstream.ok) {
    const tags = path.startsWith("/api/v1/categories") ? ["recipes", "categories"] : ["recipes"];
    for (const tag of tags) {
      try { await dependencies.invalidate(tag); }
      catch {
        // Never expose provider messages, credentials or request data; the mutation is committed.
        console.warn("Recipe cache revalidation failed after successful mutation", { tag });
      }
    }
  }
  const responseHeaders = new Headers({ "Cache-Control": "no-store" });
  for (const name of ["Content-Type", "ETag", "Location"])
    if (upstream.headers.has(name)) responseHeaders.set(name, upstream.headers.get(name)!);
  return new Response(upstream.body, { status: upstream.status, headers: responseHeaders });
}
