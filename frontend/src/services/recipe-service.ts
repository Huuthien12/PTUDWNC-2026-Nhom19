import { apiClient } from "@/services/api-client";
import type { RecipePage } from "@/types/recipe";

export function getRecipes(page = 1, pageSize = 12): Promise<RecipePage> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return apiClient<RecipePage>(`/api/v1/recipes?${query.toString()}`, { cache: "no-store" });
}
