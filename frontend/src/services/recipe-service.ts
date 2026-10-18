import { apiClient } from "./api-client";
import { getSession } from "./auth-session";
import { publicRecipeRequest } from "./recipe-requests";
import type { CreateRecipeRequest, RecipeDashboardPage, RecipeDetail, RecipeDto, RecipeListFilters, UpdateRecipeRequest } from "../types/recipe";
import type { RecipePage } from "../types/recipe";

export type RecipeLifecycleAction = "publish" | "unpublish" | "archive";

export function getRecipes(page = 1, pageSize = 12, filters: RecipeListFilters = {}): Promise<RecipePage> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (filters.categoryId) query.set("categoryId", filters.categoryId);
  if (filters.difficulty !== undefined) query.set("difficulty", String(filters.difficulty));
  if (filters.maxCookTime !== undefined) query.set("maxCookTime", String(filters.maxCookTime));
  if (filters.sortBy) query.set("sortBy", filters.sortBy);
  if (filters.sortOrder) query.set("sortOrder", filters.sortOrder);
  const endpoint = `/api/v1/recipes?${query}`;
  if (getSession()) return apiClient<RecipePage>(endpoint, { auth: true, cache: "no-store" });
  return publicRecipeRequest(endpoint, () => apiClient<RecipePage>(endpoint, { cache: "no-store" }));
}

export function getMyRecipes(page = 1, pageSize = 12): Promise<RecipeDashboardPage> {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  return apiClient<RecipeDashboardPage>(`/api/v1/recipes/mine?${query}`, {
    auth: true,
    cache: "no-store",
  });
}

export function createRecipe(input: CreateRecipeRequest): Promise<RecipeDto> {
  return apiClient<RecipeDto>("/api/v1/recipes", {
    method: "POST", auth: true, body: JSON.stringify(input),
  });
}

export function getRecipeBySlug(slug: string): Promise<RecipeDetail> {
  const endpoint = `/api/v1/recipes/${encodeURIComponent(slug)}`;
  if (getSession()) return apiClient<RecipeDetail>(endpoint, { auth: true, cache: "no-store" });
  return publicRecipeRequest(endpoint, () => apiClient<RecipeDetail>(endpoint, { cache: "no-store" }));
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
