import type { RecipeDetail } from "@/types/recipe";
import { apiClient } from "@/services/api-client";
import { getSession } from "@/services/auth-session";

export async function getRecipeBySlug(
  slug: string
): Promise<RecipeDetail> {
  return apiClient<RecipeDetail>(`/api/v1/recipes/${encodeURIComponent(slug)}`, {
    auth: getSession() !== null,
  });
}
