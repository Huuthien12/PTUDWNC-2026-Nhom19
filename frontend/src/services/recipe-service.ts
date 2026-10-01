import { apiClient } from "./api-client";
import { getSession } from "./auth-session";
import type { CreateRecipeRequest, RecipeDetail, RecipeDto, UpdateRecipeRequest } from "../types/recipe";
import type { RecipePage } from "../types/recipe";

export type RecipeLifecycleAction = "publish" | "unpublish" | "archive";

export function getRecipes(page = 1, pageSize = 12): Promise<RecipePage> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return apiClient<RecipePage>(`/api/v1/recipes?${query}`, { auth: getSession() !== null, cache: "no-store" });
}

export function createRecipe(input: CreateRecipeRequest): Promise<RecipeDto> {
  return apiClient<RecipeDto>("/api/v1/recipes", {
    method: "POST", auth: true, body: JSON.stringify(input),
  });
}

export function getRecipeBySlug(slug: string): Promise<RecipeDetail> {
  return apiClient<RecipeDetail>(`/api/v1/recipes/${encodeURIComponent(slug)}`, {
    auth: getSession() !== null,
  });
}

export function updateRecipe(id: string, input: UpdateRecipeRequest): Promise<RecipeDto> {
  return apiClient<RecipeDto>(`/api/v1/recipes/${encodeURIComponent(id)}`, {
    method: "PUT", auth: true, body: JSON.stringify(input),
  });
}

export function changeRecipeLifecycle(id: string, action: RecipeLifecycleAction, rowVersion: string): Promise<RecipeDto> {
  return apiClient<RecipeDto>(`/api/v1/recipes/${encodeURIComponent(id)}/${action}`, {
    method: "PATCH", auth: true, body: JSON.stringify({ rowVersion }),
  });
}

export function deleteRecipe(id: string, rowVersion: string): Promise<void> {
  return apiClient<void>(`/api/v1/recipes/${encodeURIComponent(id)}`, {
    method: "DELETE", auth: true, body: JSON.stringify({ rowVersion }),
  });
}
