export function recipeProxyPath(endpoint: string, method = "GET"): boolean {
  const path = endpoint.split("?")[0];
  return /^\/api\/v1\/recipes(?:\/[^/]+)*$/.test(path) ||
    (method !== "GET" && /^\/api\/v1\/categories(?:\/[^/]+)?$/.test(path)) ||
    (method === "PATCH" && path === "/api/v1/auth/me");
}

export function publicRecipeCache(path: string, method: string, authenticated: boolean) {
  if (authenticated || method !== "GET") return null;
  if (path === "/api/v1/recipes") return { revalidate: 120, tags: ["recipes"] };
  // Detail must reach the backend access/status check even after direct API mutations.
  return null;
}
