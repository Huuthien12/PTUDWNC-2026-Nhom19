import { apiClient } from "./api-client";
import type { RecipeSearchPage } from "@/types/search";

export function searchRecipes(query: string, page = 1, pageSize = 12): Promise<RecipeSearchPage> {
  const params = new URLSearchParams({ q: query, page: String(page), pageSize: String(pageSize) });
  return apiClient<RecipeSearchPage>(`/api/v1/recipes/search?${params}`, { cache: "no-store" });
}
