import { apiClient } from "./api-client";
import type { RecipeSearchPage } from "@/types/search";

export type RecipeSuggestion = { text: string; type: "recipe" | "category"; slug: string | null };
export function getRecipeSuggestions(query: string, signal?: AbortSignal): Promise<{ items: RecipeSuggestion[] }> {
  return apiClient<{ items: RecipeSuggestion[] }>(`/api/v1/recipes/suggestions?q=${encodeURIComponent(query)}`, { cache: "no-store", signal });
}

export function searchRecipes(query: string, page = 1, pageSize = 12): Promise<RecipeSearchPage> {
  const params = new URLSearchParams({ q: query, page: String(page), pageSize: String(pageSize) });
  return apiClient<RecipeSearchPage>(`/api/v1/recipes/search?${params}`, { cache: "no-store" });
}
